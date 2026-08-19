# Kế hoạch Triển khai Code Module Auth / Identity — ArtCommission (Dillustration)

Tài liệu này chi tiết hóa kế hoạch triển khai (Implementation Plan) cho **Module Auth / Identity** dựa trên sơ đồ [`Dillustration_ERD.drawio`](Dillustration_ERD.drawio), kiến trúc Monolith Clean Architecture (.NET 8) trong [`docs/01-architecture.md`](01-architecture.md), và danh sách Use Cases chuẩn trong [`docs/05-use-cases.md`](05-use-cases.md).

---

> 📌 **QUY ĐỊNH BẮT BUỘC KHI THỰC THI (WORKFLOW RULES):**
> 1. **Branch Naming**: Nhánh làm việc cho module Auth phải đính kèm Mã định danh/Tên người làm `de190123` theo chuẩn:
>    `feature/de190123-auth-identity-setup` (hoặc `chore/de190123-update-docs-auth`).
> 2. **Review & Approval Gate từng Giai đoạn**: Khi kết thúc mỗi Bước/Giai đoạn trong Kế hoạch (từ Bước 1 đến Bước 5):
>    - Thực hiện `dotnet build` kiểm tra thành công (`0 Error(s)`).
>    - Dừng lại, báo cáo kết quả và trình commit cho User kiểm tra.
>    - **CHỈ KHI USER DUYỆT MỚI ĐƯỢC CHUYỂN SANG BƯỚC TIẾP THEO.**
> 3. **Commit Authoring**: Tất cả các commit tuyệt đối **KHÔNG đính kèm dòng `Co-authored-by`** (chỉ giữ thông tin tác giả duy nhất của người dùng).


---

## 1. Mục tiêu & Phạm vi Use Cases


Module Auth chịu trách nhiệm cho các Use Cases sau:
- **UC-01: Register & Verify Account**: Đăng ký tài khoản mới, mã hóa mật khẩu, gửi email/OTP xác thực tài khoản (`IsVerified = true`).
- **UC-02: Authenticate & Manage Session**: Đăng nhập (JWT Access Token 15 phút + Refresh Token 7 ngày với cơ chế Rotation & Revocation), lấy thông tin user hiện tại (`/me`), Đăng xuất (`RevokeToken`).
- **UC-03: Forgot / Reset Password**: Yêu cầu mã token khôi phục mật khẩu qua email và đặt lại mật khẩu mới.
- **UC-04: Manage Profile & Security Settings**: Đổi mật khẩu, xem và cập nhật thông tin cá nhân.

---

## 2. Chi tiết Cấu trúc Entity (Domain Layer)

### 2.1 `ApplicationUser.cs` (`src/ArtCommission.Domain/Entities/Identity/`)
Ke thừa từ ASP.NET Core `IdentityUser<Guid>`:
```csharp
namespace ArtCommission.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Navigation Properties
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
```

### 2.2 `RefreshToken.cs` (`src/ArtCommission.Domain/Entities/Identity/`)
Ke thừa từ `BaseEntity`:
```csharp
namespace ArtCommission.Domain.Entities.Identity;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;

    // Navigation Property
    public virtual ApplicationUser User { get; set; } = null!;
}
```

### 2.3 Enumeration & Roles (`src/ArtCommission.Domain/Enums/`)
- Roles hỗ trợ: `Administrator`, `Moderator`, `Creator`, `Client`.
- Hỗ trợ **Multi-role User**: 1 `ApplicationUser` có thể gán nhiều Roles trong bảng `AspNetUserRoles`.

---

## 3. Phân chia Code theo 4 Layer Clean Architecture

```
src/
├── ArtCommission.Domain/
│   ├── Entities/Identity/
│   │   ├── ApplicationUser.cs
│   │   └── RefreshToken.cs
│   └── Enums/
│       └── UserRole.cs
│
├── ArtCommission.Application/
│   ├── Common/
│   │   ├── Interfaces/
│   │   │   ├── IIdentityService.cs
│   │   │   ├── IJwtTokenGenerator.cs
│   │   │   └── IEmailService.cs
│   │   └── DTOs/
│   │       ├── AuthResponseDto.cs
│   │       ├── UserDto.cs
│   │       └── TokenDto.cs
│   └── Auth/
│       ├── Commands/
│       │   ├── Register/ (RegisterCommand, RegisterCommandHandler, RegisterCommandValidator)
│       │   ├── Login/ (LoginCommand, LoginCommandHandler, LoginCommandValidator)
│       │   ├── RefreshToken/ (RefreshTokenCommand, RefreshTokenCommandHandler)
│       │   ├── RevokeToken/ (RevokeTokenCommand, RevokeTokenCommandHandler)
│       │   ├── ForgotPassword/ (ForgotPasswordCommand, ForgotPasswordCommandHandler)
│       │   └── ResetPassword/ (ResetPasswordCommand, ResetPasswordCommandHandler)
│       └── Queries/
│           └── GetCurrentUser/ (GetCurrentUserQuery, GetCurrentUserQueryHandler)
│
├── ArtCommission.Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs (Đăng ký DbSet<RefreshToken>, Extend IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>)
│   │   └── Configurations/
│   │       ├── ApplicationUserConfiguration.cs
│   │       └── RefreshTokenConfiguration.cs
│   ├── Identity/
│   │   ├── IdentityService.cs (Triển khai IIdentityService dùng UserManager & SignInManager)
│   │   └── JwtTokenGenerator.cs (Triển khai IJwtTokenGenerator tạo AccessToken mang Claims: sub, email, role, name, jti)
│   └── ExternalServices/
│       └── Email/ (EmailService.cs gửi OTP / Email verification)
│
└── ArtCommission.API/
    ├── Controllers/
    │   └── AuthController.cs
    └── Program.cs (Cấu hình Identity, JWT Bearer Auth, Swagger Security Definition)
```

---

## 4. Chi tiết API Endpoints (`AuthController.cs`)

| HTTP Method | Route | Request Body / Query | Access | Description |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | `RegisterRequestDto` | Public | Đăng ký tài khoản mới (Guest) |
| `POST` | `/api/v1/auth/login` | `LoginRequestDto` | Public | Đăng nhập lấy JWT & RefreshToken |
| `POST` | `/api/v1/auth/refresh-token` | `RefreshTokenRequestDto` | Public | Đổi RefreshToken mới (Token Rotation) |
| `POST` | `/api/v1/auth/revoke-token` | `RevokeTokenRequestDto` | Authorized | Thu hồi RefreshToken (Logout) |
| `POST` | `/api/v1/auth/forgot-password` | `ForgotPasswordRequestDto` | Public | Yêu cầu gửi email khôi phục mật khẩu |
| `POST` | `/api/v1/auth/reset-password` | `ResetPasswordRequestDto` | Public | Đặt lại mật khẩu bằng Token khôi phục |
| `GET` | `/api/v1/auth/me` | - | Authorized | Lấy thông tin User hiện tại kèm danh sách Roles |

---

## 5. Kế hoạch Các bước Thực thi (Database-First Workflow Checklist)

### 🔹 Bước 1: Tạo Script SQL DDL Khởi tạo Bảng trong SQL Server (Database-First)
- [ ] Tạo file script `scripts/01_auth_identity.sql` chứa mã SQL DDL tạo các bảng `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `RefreshTokens` kèm chỉ mục `UNIQUE INDEX` và khóa ngoại `FOREIGN KEY`.
- [ ] Thực thi script `01_auth_identity.sql` trên cơ sở dữ liệu SQL Server.
- [ ] 🛑 **CheckPoint Commit & Approval 1**: Trình commit `feat(auth): add Database-First SQL Server DDL scripts for Auth tables` $\rightarrow$ **Chờ User duyệt mới sang Bước 2**.

### 🔹 Bước 2: Khởi tạo Domain Entities & Interfaces (Domain + Application Layer)
- [ ] Tạo `ApplicationUser.cs` và `RefreshToken.cs` trong `ArtCommission.Domain/Entities/Identity/` khớp 100% cấu trúc các bảng SQL Server.
- [ ] Khai báo `IIdentityService.cs`, `IJwtTokenGenerator.cs`, `IEmailService.cs` trong `Application/Common/Interfaces/`.
- [ ] Tạo DTOs: `AuthResponseDto`, `UserDto`, `RefreshTokenDto`.
- [ ] `dotnet build` kiểm tra thành công.
- [ ] 🛑 **CheckPoint Commit & Approval 2**: Trình commit `feat(auth): add identity entities and core service interfaces` $\rightarrow$ **Chờ User duyệt mới sang Bước 3**.

### 🔹 Bước 3: Cấu hình EF Core Database-First Persistence Mapping (Infrastructure Layer)
- [ ] Cập nhật `AppDbContext.cs` thừa kế `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.
- [ ] Thêm Fluent API configuration mapping khớp bảng SQL Server đã tạo:
  - Mapping `ApplicationUser` $\rightarrow$ Bảng `AspNetUsers`
  - Mapping `RefreshToken` $\rightarrow$ Bảng `RefreshTokens`
- [ ] `dotnet build` kiểm tra thành công.
- [ ] 🛑 **CheckPoint Commit & Approval 3**: Trình commit `feat(auth): configure EF Core Database-First entity mappings` $\rightarrow$ **Chờ User duyệt mới sang Bước 4**.


### 🔹 Bước 4: Viết CQRS Commands & Queries (Application Layer)
- [ ] Implement `RegisterCommand` + `RegisterCommandValidator` (FluentValidation: email hợp lệ, mật khẩu mạnh).
- [ ] Implement `LoginCommand` + `LoginCommandValidator`.
- [ ] Implement `RefreshTokenCommand` (Guard reuse token: nếu nhận diện token bị vỡ chuỗi rotation -> revoke toàn bộ chain).
- [ ] Implement `RevokeTokenCommand` & `ForgotPasswordCommand` & `ResetPasswordCommand`.
- [ ] Implement `GetCurrentUserQuery`.
- [ ] `dotnet build` kiểm tra thành công.
- [ ] 🛑 **CheckPoint Commit & Approval 4**: Trình commit `feat(auth): add CQRS commands, queries and validators for authentication` $\rightarrow$ **Chờ User duyệt mới sang Bước 5**.

### 🔹 Bước 5: Cấu hình Web API & Middleware (API Layer)
- [ ] Đăng ký Authentication Services trong `Program.cs`: `AddIdentity`, `AddJwtBearer` với Issuer, Audience, Secret Key từ `appsettings.json`.
- [ ] Tạo `AuthController.cs` nhận Requests và gửi qua MediatR `ISender`.
- [ ] Cấu hình Swagger/Swashbuckle hỗ trợ nhập `Bearer <token>` khi test API.
- [ ] `dotnet build` kiểm tra thành công.
- [ ] 🛑 **CheckPoint Commit & Approval 5**: Trình commit `feat(auth): expose AuthController endpoints and configure JWT bearer middleware` $\rightarrow$ **Chờ User duyệt mới hoàn tất**.

### 🔹 Bước 6: Kiểm thử & Xác minh Toàn diện (Verification)
- [ ] Test API qua Swagger / REST File:
  - Đăng ký tài khoản `Client` và `Creator`.
  - Đăng nhập thử nghiệm JWT.
  - Test Refresh Token Rotation và Revocation.
  - Test endpoint `/api/v1/auth/me` với Bearer Token.
- [ ] 🛑 **Tạo PR (Pull Request)** từ `feature/de190123-auth-identity-setup` vào `main` $\rightarrow$ **Chờ User Review & Merge**.


---

## 6. Quy tắc Bảo mật & Concurrency

1. **Security**:
   - Refresh Token **không được lưu dưới dạng plaintext** trong DB -> Luôn mã hóa SHA256 Hash trước khi lưu vào `RefreshTokens.TokenHash`.
   - Access Token có thời hạn ngắn (**15 phút**), Refresh Token (**7 ngày**).
   - Phát hiện token reuse -> Ngay lập tức hủy (`RevokedAt`) toàn bộ Refresh Tokens của User đó.
2. **Multi-Role Handling**:
   - Khi phát hành JWT Token, duyệt toàn bộ danh sách Roles của User và add nhiều Claim `ClaimTypes.Role` để hỗ trợ authorize đa vai trò ở Frontend & Backend.

---

## 7. Cấu hình Deployment & Production Readiness (Triển khai Thực tế)

### 7.1 Quản lý Biến Môi trường & Secrets (Environment Variables)
Để đảm bảo an toàn khi deploy lên Server / Cloud (Azure App Service, Docker, VPS), **tuyệt đối không hardcode secrets** trong `appsettings.json`. Sử dụng biến môi trường:

| Config Key | Environment Variable Name | Purpose | Example Value (Production) |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Chuỗi kết nối DB SQL Server | `Server=tcp:sqlserver...;Database=DilDb;...` |
| `Jwt:SecretKey` | `Jwt__SecretKey` | Khóa bí mật ký mã hóa JWT Token (Min 256 bits) | `Complex_Production_JWT_Secret_Key_2026_@Secured` |
| `Jwt:Issuer` | `Jwt__Issuer` | Đơn vị phát hành JWT Token | `https://api.dillustration.com` |
| `Jwt:Audience` | `Jwt__Audience` | Đối tượng sử dụng JWT Token | `https://dillustration.com` |
| `EmailSettings:ApiKey` | `EmailSettings__ApiKey` | API Key gửi Email xác thực | `SG.Production_SendGrid_Key...` |

### 7.2 Tự động Migrate Database & Initial Data Seeding khi Deploy
Khi ứng dụng khởi chạy ở môi trường Staging/Production, hệ thống tự động:
1. Thực thi `Database.MigrateAsync()` để áp dụng các Migrations EF Core mới nhất.
2. Tự động khởi tạo (Seed) **4 Roles hệ thống**: `Administrator`, `Moderator`, `Creator`, `Client`.
3. Tự động khởi tạo **Super Admin Account mặc định** nếu chưa tồn tại (đọc thông tin từ biến môi trường `SEED_ADMIN_EMAIL` & `SEED_ADMIN_PASSWORD`).

```csharp
// Program.cs - Automatic Deployment Seeding Scope
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    await dbContext.Database.MigrateAsync();
    await IdentityDataSeeder.SeedRolesAndAdminAsync(roleManager, userManager);
}
```

### 7.3 Cấu hình CORS & Cookie Security Policies
- **CORS**: Chỉ cho phép Origin của Frontend (VD: `https://dillustration.com`, `https://staging.dillustration.com`). Không dùng `AllowAnyOrigin()` ở Production.
- **Refresh Token Cookie**: Đóng gói Refresh Token trong `HttpOnly`, `Secure` (yêu cầu HTTPS), `SameSiteMode.Strict` cookie để bảo vệ chống tấn công XSS & CSRF.

### 7.4 Multi-Stage Dockerfile & CI/CD Deployment
Hỗ trợ đóng gói Docker để deploy lên Azure App Service / Docker Swarm / Kubernetes:

```dockerfile
# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ArtCommission.API/ArtCommission.API.csproj", "ArtCommission.API/"]
COPY ["src/ArtCommission.Application/ArtCommission.Application.csproj", "ArtCommission.Application/"]
COPY ["src/ArtCommission.Domain/ArtCommission.Domain.csproj", "ArtCommission.Domain/"]
COPY ["src/ArtCommission.Infrastructure/ArtCommission.Infrastructure.csproj", "ArtCommission.Infrastructure/"]
RUN dotnet restore "ArtCommission.API/ArtCommission.API.csproj"
COPY src/ .
WORKDIR "/src/ArtCommission.API"
RUN dotnet build "ArtCommission.API.csproj" -c Release -o /app/build
RUN dotnet publish "ArtCommission.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArtCommission.API.dll"]
```

