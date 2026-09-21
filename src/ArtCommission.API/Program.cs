using System.Text;
using ArtCommission.API.BackgroundWorkers;
using ArtCommission.API.Common;
using ArtCommission.API.Hubs;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Auth.Commands.Register;
using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Application.Revenue.Common;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Infrastructure.ExternalServices.Gemini;
using ArtCommission.Infrastructure.ExternalServices.PayOs;
using ArtCommission.Infrastructure.Identity;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using PayOS;
using PayOsOptions = ArtCommission.Infrastructure.ExternalServices.PayOs.PayOsOptions;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Nạp khoá Gemini từ .env ở thư mục cha của repo Code.
//
// VÌ SAO phải tự nạp: ASP.NET Core KHÔNG đọc file .env. Cả nhóm để khoá dịch vụ
// ngoài (payOS, Gemini) trong một .env chung ở D:\SEP490_Dillustration\.env, nên
// app phải tự tìm và đọc file đó thì khoá mới có hiệu lực.
//
// PHẠM VI CỐ Ý HẸP: chỉ lấy khoá Gemini. Các khoá payOS vẫn đọc từ User Secrets /
// biến môi trường như trước — nạp thêm .env cho payOS sẽ âm thầm đổi hành vi module
// thanh toán của người khác, không nằm trong phạm vi task này.
//
// Ưu tiên: nếu Gemini:ApiKey đã có từ User Secrets / biến môi trường thì GIỮ NGUYÊN,
// không ghi đè bằng .env.
// ---------------------------------------------------------------------------
var geminiApiKeyFromEnv = ReadGeminiApiKeyFromEnvFile(builder.Environment.ContentRootPath);

if (!string.IsNullOrWhiteSpace(geminiApiKeyFromEnv)
    && string.IsNullOrWhiteSpace(builder.Configuration[$"{GeminiOptions.SectionName}:ApiKey"]))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [$"{GeminiOptions.SectionName}:ApiKey"] = geminiApiKeyFromEnv
    });
}

// 1. Add Infrastructure Persistence & DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=ArtCommissionDb;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. Add Custom Services
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IEmailService, EmailService>();

// 3b. Payment module — DbContext exposed qua interface cho tầng Application
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IPaymentSettlementService, PaymentSettlementService>();
builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();

// 3d. Job nền đối soát thanh toán (bù trường hợp webhook không tới)
builder.Services.Configure<PaymentReconciliationSettings>(
    builder.Configuration.GetSection(PaymentReconciliationSettings.SectionName));
builder.Services.AddHostedService<PaymentReconciliationWorker>();

// 3c. Cổng thanh toán payOS
// Secret lấy từ User Secrets (dev) hoặc biến môi trường PayOS__ClientId / PayOS__ApiKey / PayOS__ChecksumKey.
builder.Services.Configure<PayOsOptions>(builder.Configuration.GetSection(PayOsOptions.SectionName));

builder.Services.AddSingleton<PayOSClient>(sp =>
{
    var payOsOptions = sp.GetRequiredService<IOptions<PayOsOptions>>().Value;
    if (!payOsOptions.IsConfigured)
    {
        // Không ném lỗi ở đây để app vẫn khởi động được khi chưa cấu hình payOS
        // (VD: chạy migration, chạy các module khác). Lỗi sẽ lộ ra khi gọi API thanh toán.
        var startupLogger = sp.GetRequiredService<ILogger<Program>>();
        startupLogger.LogWarning(
            "payOS chưa được cấu hình đủ (PayOS:ClientId / PayOS:ApiKey / PayOS:ChecksumKey). " +
            "Các endpoint thanh toán sẽ báo lỗi cho tới khi cấu hình xong.");
    }

    return new PayOSClient(new PayOSOptions
    {
        ClientId = payOsOptions.ClientId,
        ApiKey = payOsOptions.ApiKey,
        ChecksumKey = payOsOptions.ChecksumKey,
        TimeoutMs = payOsOptions.TimeoutMs,
        MaxRetries = payOsOptions.MaxRetries,
        Logger = sp.GetService<ILogger<PayOSClient>>()
    });
});

builder.Services.AddScoped<IPaymentGateway, PayOsPaymentGateway>();

// 3e. UC45 — Notification: ghi DB + đẩy SignalR real-time
builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();

// 3f. UC50 — Voucher: kiểm tra mã dùng chung (checkout, redeem)
builder.Services.AddScoped<IVoucherCheckService, VoucherCheckService>();

// 3g. UC32–UC35 — Auction: cọc đấu giá + chốt phiên (dùng chung IWalletService)
builder.Services.AddScoped<IAuctionMoneyService, AuctionMoneyService>();
builder.Services.AddScoped<IAuctionSettlementService, AuctionSettlementService>();

// 3h. UC43 — Workroom Chat: phân giải phòng, kiểm quyền thành viên
builder.Services.AddScoped<IChatRoomService, ChatRoomService>();

// 3i. Lưu file: chat attachment + link tải file gốc có hạn
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

// Chuẩn hoá đường dẫn lưu file thành ABSOLUTE ngay lúc cấu hình.
// VÌ SAO: middleware static files và FileStorageService phải trỏ vào CÙNG một thư mục.
// Nếu mỗi bên tự ghép đường dẫn tương đối theo một gốc khác nhau (ContentRootPath so với
// CurrentDirectory) thì file ghi vào chỗ này nhưng được phục vụ ở chỗ khác ⇒ link 404.
builder.Services.PostConfigure<StorageOptions>(options =>
{
    if (!Path.IsPathRooted(options.LocalRootPath))
    {
        options.LocalRootPath = Path.Combine(
            builder.Environment.ContentRootPath, options.LocalRootPath);
    }
});

builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// 3j. UC44/UC46 — AI Assistant dùng Google AI Studio (Gemini)
// Key lấy từ biến môi trường Gemini__ApiKey hoặc User Secrets; KHÔNG để trong appsettings.json.
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));

// Một instance GeminiAiClient phục vụ CẢ chatbot và dịch tin nhắn: cùng một endpoint,
// cùng một key. Đăng ký typed client để HttpClient được quản lý vòng đời đúng cách.
builder.Services.AddHttpClient<GeminiAiClient>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int?>($"{GeminiOptions.SectionName}:TimeoutSeconds") ?? 45;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

builder.Services.AddScoped<IAiChatClient>(sp => sp.GetRequiredService<GeminiAiClient>());
builder.Services.AddScoped<ITranslationClient>(sp => sp.GetRequiredService<GeminiAiClient>());

builder.Services.AddScoped<IAiContextBuilder, AiContextBuilder>();
builder.Services.AddScoped<IDeadlineRiskPredictor, DeadlineRiskPredictor>();

// 3k. UC51 — Creator Revenue Analytics: đọc sổ cái ví
builder.Services.AddScoped<IRevenueQueryService, RevenueQueryService>();

// 4. Add MediatR & FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(RegisterCommand).Assembly);

// 5. Add JWT Bearer Authentication
var secretKey = builder.Configuration["Jwt:SecretKey"] ?? "Default_Secret_Key_For_Development_Only_Must_Be_Long_256_Bits";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "ArtCommissionAPI";
var audience = builder.Configuration["Jwt:Audience"] ?? "ArtCommissionClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // SignalR handshake qua WebSocket KHÔNG gửi được header Authorization,
    // nên client phải truyền token qua query-string: /hubs/notifications?access_token=...
    // Chỉ đọc từ query-string cho đúng route hub, không nới lỏng cho REST.
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiErrors.Create(401, "Unauthorized",
                "A valid access token is required.", context.HttpContext.TraceIdentifier));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiErrors.Create(403, "Forbidden",
                "You do not have permission to access this resource.", context.HttpContext.TraceIdentifier));
        },
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

// 6. Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "https://dillustration-api.netlify.app"
            )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    // Policy riêng cho Scalar docs page fetch openapi.json (không cần credentials)
    options.AddPolicy("AllowScalarDocs", policy =>
    {
        policy.WithOrigins(
                "https://dillustration-api.netlify.app"
            )
              .AllowAnyHeader()
              .WithMethods("GET")
              .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

// 7. Add Controllers & OpenAPI (Swashbuckle + Scalar)
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(entry => entry.Key,
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());
        return new BadRequestObjectResult(ApiErrors.Create(400, "Bad Request",
            "One or more validation errors occurred.", context.HttpContext.TraceIdentifier, errors));
    };
});
builder.Services.AddEndpointsApiExplorer();

// 7b. UC45 — SignalR cho Notification Center (hub ở API/Hubs/NotificationHub.cs)
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ArtCommission (Dillustration) API",
        Version = "v1",
        Description = "REST API for the Dillustration art commission platform. " +
                      "JWT Bearer authentication required for protected endpoints.",
        Contact = new OpenApiContact
        {
            Name = "Dillustration Team",
            Email = "support@dillustration.art"
        }
    });

    // Dùng Http scheme chuẩn (bearer) thay vì ApiKey — Scalar hiểu đúng hơn
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Nhập token vào ô bên dưới (không cần prefix \"Bearer \").",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Register DbContext (SQL Server or InMemory fallback)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connStr))
    {
        options.UseSqlServer(connStr);
    }
    else
    {
        options.UseInMemoryDatabase("ArtCommissionDb");
    }
});

// Register Application Services
builder.Services.AddScoped<ICommissionService, CommissionService>();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
    var (status, title, detail) = exception switch
    {
        DbUpdateConcurrencyException => (409, "Conflict", "This commission changed while your request was being processed. Please retry."),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, "Conflict", "A record with the same key already exists."),
        KeyNotFoundException => (404, "Not Found", exception.Message),
        UnauthorizedAccessException => (403, "Forbidden", exception.Message),
        ArgumentException => (400, "Bad Request", exception.Message),
        InvalidOperationException => (400, "Bad Request", exception.Message),
        _ => (500, "Internal Server Error", "An unexpected error occurred.")
    };
    app.Logger.LogError(exception, "API request failed with status {StatusCode}", status);
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(ApiErrors.Create(status, title, detail, context.TraceIdentifier));
}));

// Auto-initialize Database & Seed Roles on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = services.GetRequiredService<AppDbContext>();

        // Áp dụng migration EF Core đang chờ (thay cho EnsureCreatedAsync).
        // LƯU Ý: DB dev tạo bằng EnsureCreatedAsync KHÔNG có bảng __EFMigrationsHistory
        // => phải xoá DB một lần rồi chạy lại để migration áp dụng được từ đầu.
        await dbContext.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var roles = new[] { "Administrator", "Moderator", "Creator", "Client" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
        logger.LogInformation("Database initialized and default roles seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

// Configure HTTP request pipeline
// openapi/v1.json luôn public (cả Production) để GitHub Actions export & Scalar Netlify fetch được
app.UseSwagger(options =>
{
    options.RouteTemplate = "openapi/{documentName}.json";
});

if (app.Environment.IsDevelopment())
{
    // Swagger UI giữ nguyên cho dev
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "ArtCommission API v1");
        c.RoutePrefix = "swagger";
    });

    // Scalar UI — UI đẹp hơn cho dev local
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Dillustration API Docs")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithOpenApiRoutePattern("/openapi/{documentName}.json");
    });
}

app.UseHttpsRedirection();

// File đính kèm chat và file bàn giao được phục vụ từ thư mục Storage:LocalRootPath,
// KHÔNG phụ thuộc wwwroot (thư mục này có thể không tồn tại trong repo).
// Dùng PhysicalFileProvider trỏ thẳng vào thư mục upload đã chuẩn hoá absolute:
//  - tạo thư mục nếu chưa có, tránh lỗi khởi động ở máy mới clone;
//  - không bật directory browsing nên chỉ truy cập được file có đường dẫn chính xác.
var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;

if (!string.IsNullOrWhiteSpace(storageOptions.LocalRootPath))
{
    Directory.CreateDirectory(storageOptions.LocalRootPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(storageOptions.LocalRootPath),
        RequestPath = storageOptions.PublicBasePath
    });
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// UC45 — SignalR hub. Client kết nối: /hubs/notifications?access_token={jwt}
app.MapHub<NotificationHub>("/hubs/notifications");

// UC43 — SignalR hub chat. Client kết nối: /hubs/chat?access_token={jwt}
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

/// <summary>
/// Tìm file .env ở thư mục cha và đọc giá trị khoá <c>Gemini-Ai-Key</c>.
///
/// Tìm ngược lên tối đa 6 cấp từ thư mục chạy. Con số này không tuỳ tiện:
/// .env nằm ở gốc workspace, cách ContentRootPath (src/ArtCommission.API) ĐÚNG 5 cấp
///   ArtCommission.API → src → dil-backend → Code → SEP490_Dillustration.
/// Để 6 để còn dư một cấp khi repo được đặt sâu hơn, mà vẫn không quét ngược ra
/// ngoài workspace.
///
/// Trả về null nếu không có file / không có khoá. KHÔNG log giá trị khoá.
/// </summary>
static string? ReadGeminiApiKeyFromEnvFile(string startDirectory)
{
    const string keyName = "Gemini-Ai-Key";
    const int maxDepth = 6;

    var directory = new DirectoryInfo(startDirectory);

    for (var depth = 0; depth < maxDepth && directory is not null; depth++)
    {
        var candidate = Path.Combine(directory.FullName, ".env");

        if (File.Exists(candidate))
        {
            foreach (var rawLine in File.ReadAllLines(candidate))
            {
                var line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var name = line[..separator].Trim();
                if (!string.Equals(name, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = line[(separator + 1)..].Trim().Trim('"', '\'');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        directory = directory.Parent;
    }

    return null;
}
