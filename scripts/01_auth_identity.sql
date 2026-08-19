-- =============================================================================
-- Database-First SQL Server DDL Script: Full Auth & Identity Module (8 Tables)
-- Description: Creates Users, Roles, UserRoles, UserClaims, RoleClaims, UserLogins, UserTokens, RefreshTokens
-- Project: ArtCommission (Dillustration)
-- =============================================================================

-- 1. Bảng Users (Tương đương AspNetUsers)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserName] NVARCHAR(256) NOT NULL,
        [NormalizedUserName] NVARCHAR(256) NOT NULL,
        [Email] NVARCHAR(256) NOT NULL,
        [NormalizedEmail] NVARCHAR(256) NOT NULL,
        [EmailConfirmed] BIT NOT NULL DEFAULT 0,
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(MAX) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
        [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
        [LockoutEnd] DATETIMEOFFSET NULL,
        [LockoutEnabled] BIT NOT NULL DEFAULT 1,
        [AccessFailedCount] INT NOT NULL DEFAULT 0,
        [FullName] NVARCHAR(150) NOT NULL,
        [IsVerified] BIT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [UpdatedAt] DATETIMEOFFSET NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_NormalizedEmail] ON [dbo].[Users] ([NormalizedEmail]);
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_NormalizedUserName] ON [dbo].[Users] ([NormalizedUserName]);
END;
GO

-- 2. Bảng Roles (Tương đương AspNetRoles)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Roles')
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Name] NVARCHAR(256) NULL,
        [NormalizedName] NVARCHAR(256) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UX_Roles_NormalizedName] ON [dbo].[Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
END;
GO

-- 3. Bảng UserRoles (Tương đương AspNetUserRoles - Mối quan hệ n-n giữa Users và Roles)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'UserRoles')
BEGIN
    CREATE TABLE [dbo].[UserRoles] (
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC),
        CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_UserRoles_RoleId] ON [dbo].[UserRoles] ([RoleId]);
END;
GO

-- 4. Bảng UserClaims (Tương đương AspNetUserClaims - Claim theo User)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'UserClaims')
BEGIN
    CREATE TABLE [dbo].[UserClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserClaims_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_UserClaims_UserId] ON [dbo].[UserClaims] ([UserId]);
END;
GO

-- 5. Bảng RoleClaims (Tương đương AspNetRoleClaims - Claim theo Role)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'RoleClaims')
BEGIN
    CREATE TABLE [dbo].[RoleClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_RoleClaims_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RoleClaims_RoleId] ON [dbo].[RoleClaims] ([RoleId]);
END;
GO

-- 6. Bảng UserLogins (Tương đương AspNetUserLogins - Đăng nhập OAuth Google/Facebook)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'UserLogins')
BEGIN
    CREATE TABLE [dbo].[UserLogins] (
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [ProviderKey] NVARCHAR(128) NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX) NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY CLUSTERED ([LoginProvider] ASC, [ProviderKey] ASC),
        CONSTRAINT [FK_UserLogins_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_UserLogins_UserId] ON [dbo].[UserLogins] ([UserId]);
END;
GO

-- 7. Bảng UserTokens (Tương đương AspNetUserTokens - OTP 2FA / Verification Tokens)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'UserTokens')
BEGIN
    CREATE TABLE [dbo].[UserTokens] (
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [Name] NVARCHAR(128) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY CLUSTERED ([UserId] ASC, [LoginProvider] ASC, [Name] ASC),
        CONSTRAINT [FK_UserTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

-- 8. Bảng RefreshTokens (Bảng tùy chỉnh quản lý JWT Refresh Token Rotation)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'RefreshTokens')
BEGIN
    CREATE TABLE [dbo].[RefreshTokens] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [TokenHash] NVARCHAR(450) NOT NULL,
        [ExpiresAt] DATETIMEOFFSET NOT NULL,
        [RevokedAt] DATETIMEOFFSET NULL,
        [CreatedByIp] NVARCHAR(50) NULL,
        [ReplacedByTokenHash] NVARCHAR(450) NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UX_RefreshTokens_TokenHash] ON [dbo].[RefreshTokens] ([TokenHash]);
    CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens] ([UserId]);
END;
GO
