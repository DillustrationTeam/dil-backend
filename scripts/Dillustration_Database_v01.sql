-- =============================================================================
-- Database-First SQL Server DDL Script: Full Art Commission System (24 Tables)
-- Description: Auth & Identity (8 Tables) + Business Modules (16 Tables)
-- Project: ArtCommission (Dillustration)
-- Improvements: Idempotent (IF NOT EXISTS), Unique 1-1 Constraints, Indexed Lengths, Performance Indexes
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


-- =============================================================================
-- MODULE 1: AUTH & IDENTITY MODULE (8 TABLES)
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
        [UpdatedAt] DATETIMEOFFSET NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UX_RefreshTokens_TokenHash] ON [dbo].[RefreshTokens] ([TokenHash]);
    CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens] ([UserId]);
END;
GO


-- =============================================================================
-- MODULE 2: PORTFOLIO & SEARCH MODULE
-- =============================================================================

-- 9. Bảng CreatorProfile (Quan hệ 1-1 với Users qua UNIQUE CONSTRAINT)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'CreatorProfile')
BEGIN
    CREATE TABLE [dbo].[CreatorProfile] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [DisplayName] NVARCHAR(150) NULL,
        [Bio] NVARCHAR(MAX) NULL,
        [RateCard] NVARCHAR(MAX) NULL, -- JSON RateCard Configuration
        [CommissionSlots] INT NULL DEFAULT 0,
        [RatingAvg] DECIMAL(18, 2) NULL DEFAULT 0.0,
        [IsAiVerified] BIT NULL DEFAULT 0,
        CONSTRAINT [PK_CreatorProfile] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_CreatorProfile_UserId] UNIQUE ([UserId]),
        CONSTRAINT [FK_CreatorProfile_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

-- 10. Bảng Artwork
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Artwork')
BEGIN
    CREATE TABLE [dbo].[Artwork] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CreatorId] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [ImageUrl] NVARCHAR(MAX) NOT NULL,
        [Style] NVARCHAR(100) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Published',
        CONSTRAINT [PK_Artwork] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Artwork_CreatorProfile] FOREIGN KEY ([CreatorId]) REFERENCES [dbo].[CreatorProfile] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Artwork_CreatorId_Status] ON [dbo].[Artwork] ([CreatorId], [Status]);
END;
GO

-- 11. Bảng Tag
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Tag')
BEGIN
    CREATE TABLE [dbo].[Tag] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Name] NVARCHAR(150) NOT NULL,
        [IsAiGenerated] BIT NULL DEFAULT 0,
        CONSTRAINT [PK_Tag] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_Tag_Name] UNIQUE ([Name])
    );
END;
GO

-- 12. Bảng ArtworkTag (Bảng trung gian n-n giữa Artwork và Tag)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'ArtworkTag')
BEGIN
    CREATE TABLE [dbo].[ArtworkTag] (
        [ArtworkId] UNIQUEIDENTIFIER NOT NULL,
        [TagId] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_ArtworkTag] PRIMARY KEY CLUSTERED ([ArtworkId] ASC, [TagId] ASC),
        CONSTRAINT [FK_ArtworkTag_Artwork] FOREIGN KEY ([ArtworkId]) REFERENCES [dbo].[Artwork] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ArtworkTag_Tag] FOREIGN KEY ([TagId]) REFERENCES [dbo].[Tag] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_ArtworkTag_TagId] ON [dbo].[ArtworkTag] ([TagId]);
END;
GO

-- 13. Bảng Follow (Theo dõi giữa Users)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Follow')
BEGIN
    CREATE TABLE [dbo].[Follow] (
        [FollowerId] UNIQUEIDENTIFIER NOT NULL,
        [FollowingId] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Follow] PRIMARY KEY CLUSTERED ([FollowerId] ASC, [FollowingId] ASC),
        CONSTRAINT [FK_Follow_Follower] FOREIGN KEY ([FollowerId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_Follow_Following] FOREIGN KEY ([FollowingId]) REFERENCES [dbo].[Users] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Follow_FollowingId] ON [dbo].[Follow] ([FollowingId]);
END;
GO


-- =============================================================================
-- MODULE 3: COMMISSION & SERVICES MODULE
-- =============================================================================

-- 14. Bảng CommissionService
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'CommissionService')
BEGIN
    CREATE TABLE [dbo].[CommissionService] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CreatorId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(255) NOT NULL,
        [Category] NVARCHAR(100) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Price] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active',
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_CommissionService] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CommissionService_CreatorProfile] FOREIGN KEY ([CreatorId]) REFERENCES [dbo].[CreatorProfile] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_CommissionService_CreatorId] ON [dbo].[CommissionService] ([CreatorId]);
END;
GO

-- 15. Bảng Commission
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Commission')
BEGIN
    CREATE TABLE [dbo].[Commission] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Title] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [CreatorId] UNIQUEIDENTIFIER NOT NULL,
        [ServiceId] UNIQUEIDENTIFIER NULL,
        [TotalPrice] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [EscrowStatus] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [CurrentStage] INT NOT NULL DEFAULT 1,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Created',
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Commission] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Commission_Client] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_Commission_Creator] FOREIGN KEY ([CreatorId]) REFERENCES [dbo].[CreatorProfile] ([Id]),
        CONSTRAINT [FK_Commission_Service] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[CommissionService] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Commission_ClientId_Status] ON [dbo].[Commission] ([ClientId], [Status]);
    CREATE NONCLUSTERED INDEX [IX_Commission_CreatorId_Status] ON [dbo].[Commission] ([CreatorId], [Status]);
END;
GO

-- 16. Bảng Milestone
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Milestone')
BEGIN
    CREATE TABLE [dbo].[Milestone] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CommissionId] UNIQUEIDENTIFIER NOT NULL,
        [Sequence] INT NOT NULL DEFAULT 1,
        [Title] NVARCHAR(255) NOT NULL,
        [Price] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [WipPreviewUrl] NVARCHAR(MAX) NULL,
        [SourceFilePath] NVARCHAR(MAX) NULL,
        [RevisionLimit] INT NOT NULL DEFAULT 3,
        [RevisionCount] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Milestone] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Milestone_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Milestone_CommissionId_Sequence] ON [dbo].[Milestone] ([CommissionId], [Sequence]);
END;
GO


-- =============================================================================
-- MODULE 4: PAYMENT, ESCROW & WALLET MODULE
-- =============================================================================

-- 17. Bảng Wallet
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Wallet')
BEGIN
    CREATE TABLE [dbo].[Wallet] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Balance] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [UpdatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Wallet] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_Wallet_UserId] UNIQUE ([UserId]),
        CONSTRAINT [FK_Wallet_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

-- 18. Bảng Payment
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Payment')
BEGIN
    CREATE TABLE [dbo].[Payment] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CommissionId] UNIQUEIDENTIFIER NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [EscrowStatus] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [TransactionRef] NVARCHAR(100) NULL,
        [PaidAt] DATETIMEOFFSET NULL,
        [WalletId] UNIQUEIDENTIFIER NULL,
        [PaymentMethod] NVARCHAR(50) NULL,
        CONSTRAINT [PK_Payment] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Payment_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id]),
        CONSTRAINT [FK_Payment_Wallet] FOREIGN KEY ([WalletId]) REFERENCES [dbo].[Wallet] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Payment_CommissionId] ON [dbo].[Payment] ([CommissionId]);
END;
GO

-- 19. Bảng Transaction
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Transaction')
BEGIN
    CREATE TABLE [dbo].[Transaction] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [WalletId] UNIQUEIDENTIFIER NOT NULL,
        [CommissionId] UNIQUEIDENTIFIER NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [Ref] NVARCHAR(100) NULL,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Transaction] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Transaction_Wallet] FOREIGN KEY ([WalletId]) REFERENCES [dbo].[Wallet] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Transaction_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Transaction_WalletId] ON [dbo].[Transaction] ([WalletId]);
END;
GO

-- 20. Bảng EscrowTransaction
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'EscrowTransaction')
BEGIN
    CREATE TABLE [dbo].[EscrowTransaction] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CommissionId] UNIQUEIDENTIFIER NOT NULL,
        [MilestoneId] UNIQUEIDENTIFIER NULL,
        [WalletId] UNIQUEIDENTIFIER NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [Type] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Held',
        [AutoRelease] BIT NULL DEFAULT 0,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_EscrowTransaction] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_EscrowTransaction_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id]),
        CONSTRAINT [FK_EscrowTransaction_Milestone] FOREIGN KEY ([MilestoneId]) REFERENCES [dbo].[Milestone] ([Id]),
        CONSTRAINT [FK_EscrowTransaction_Wallet] FOREIGN KEY ([WalletId]) REFERENCES [dbo].[Wallet] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_EscrowTransaction_CommissionId] ON [dbo].[EscrowTransaction] ([CommissionId]);
END;
GO

-- 21. Bảng PayoutRequest
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'PayoutRequest')
BEGIN
    CREATE TABLE [dbo].[PayoutRequest] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [WalletId] UNIQUEIDENTIFIER NOT NULL,
        [Amount] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [BankInfo] NVARCHAR(MAX) NULL,
        [RequestedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        [ProcessedAt] DATETIMEOFFSET NULL,
        CONSTRAINT [PK_PayoutRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PayoutRequest_Wallet] FOREIGN KEY ([WalletId]) REFERENCES [dbo].[Wallet] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_PayoutRequest_WalletId_Status] ON [dbo].[PayoutRequest] ([WalletId], [Status]);
END;
GO


-- =============================================================================
-- MODULE 5: CHAT, NOTIFICATION, REVIEWS & DISPUTES MODULE
-- =============================================================================

-- 22. Bảng Message
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Message')
BEGIN
    CREATE TABLE [dbo].[Message] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CommissionId] UNIQUEIDENTIFIER NOT NULL,
        [SenderId] UNIQUEIDENTIFIER NOT NULL,
        [Body] NVARCHAR(MAX) NULL,
        [SentAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        [IsRead] BIT NULL DEFAULT 0,
        CONSTRAINT [PK_Message] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Message_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Message_Sender] FOREIGN KEY ([SenderId]) REFERENCES [dbo].[Users] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Message_CommissionId_SentAt] ON [dbo].[Message] ([CommissionId], [SentAt] DESC);
END;
GO

-- 23. Bảng Notification
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Notification')
BEGIN
    CREATE TABLE [dbo].[Notification] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [IsRead] BIT NULL DEFAULT 0,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Notification_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Notification_UserId_IsRead] ON [dbo].[Notification] ([UserId], [IsRead]);
END;
GO

-- 24. Bảng Review (Quan hệ 1-1 với Commission qua UNIQUE CONSTRAINT)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Review')
BEGIN
    CREATE TABLE [dbo].[Review] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CommissionId] UNIQUEIDENTIFIER NOT NULL,
        [ReviewerId] UNIQUEIDENTIFIER NOT NULL,
        [Rating] INT NOT NULL DEFAULT 5,
        [Comment] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_Review] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_Review_CommissionId] UNIQUE ([CommissionId]),
        CONSTRAINT [FK_Review_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Review_Reviewer] FOREIGN KEY ([ReviewerId]) REFERENCES [dbo].[Users] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Review_ReviewerId] ON [dbo].[Review] ([ReviewerId]);
END;
GO

-- 25. Bảng Dispute
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Dispute')
BEGIN
    CREATE TABLE [dbo].[Dispute] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [CommissionId] UNIQUEIDENTIFIER NOT NULL,
        [RaisedBy] UNIQUEIDENTIFIER NOT NULL,
        [Reason] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Open',
        [RefundRate] DECIMAL(18, 2) NULL DEFAULT 0.00,
        [AdminNote] NVARCHAR(MAX) NULL,
        [ResolvedAt] DATETIMEOFFSET NULL,
        CONSTRAINT [PK_Dispute] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Dispute_Commission] FOREIGN KEY ([CommissionId]) REFERENCES [dbo].[Commission] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Dispute_RaisedBy] FOREIGN KEY ([RaisedBy]) REFERENCES [dbo].[Users] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Dispute_CommissionId] ON [dbo].[Dispute] ([CommissionId]);
END;
GO


-- =============================================================================
-- MODULE 6: AUDIT TRAIL MODULE
-- =============================================================================

-- 26. Bảng AuditLog
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'AuditLog')
BEGIN
    CREATE TABLE [dbo].[AuditLog] (
        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [EntityType] NVARCHAR(100) NOT NULL,
        [EntityId] UNIQUEIDENTIFIER NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [Changes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AuditLog_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AuditLog_UserId_CreatedAt] ON [dbo].[AuditLog] ([UserId], [CreatedAt] DESC);
END;
GO