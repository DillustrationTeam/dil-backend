using System.Text;
using ArtCommission.API.BackgroundWorkers;
using ArtCommission.Application.Auth.Commands.Register;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Infrastructure.ExternalServices.PayOs;
using ArtCommission.Infrastructure.Identity;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PayOS;
using PayOsOptions = ArtCommission.Infrastructure.ExternalServices.PayOs.PayOsOptions;

var builder = WebApplication.CreateBuilder(args);

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
});

// 6. Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 7. Add Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ArtCommission (Dillustration) API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

var app = builder.Build();

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
