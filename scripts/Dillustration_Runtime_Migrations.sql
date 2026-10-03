IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [PlatformConfig] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(500) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_PlatformConfig] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [IsVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [BankAccount] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [BankName] nvarchar(200) NOT NULL,
        [BankBin] nvarchar(20) NULL,
        [BankCode] nvarchar(20) NULL,
        [AccountNumber] nvarchar(50) NOT NULL,
        [AccountHolder] nvarchar(200) NOT NULL,
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_BankAccount] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankAccount_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(450) NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [RevokedAt] datetimeoffset NULL,
        [CreatedByIp] nvarchar(50) NULL,
        [ReplacedByTokenHash] nvarchar(450) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [Wallet] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [LockedBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Currency] nvarchar(3) NOT NULL DEFAULT N'VND',
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [RowVersion] rowversion NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Wallet] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Wallet_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [PaymentOrder] (
        [Id] uniqueidentifier NOT NULL,
        [OrderRef] nvarchar(50) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Gateway] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [PayOsOrderCode] bigint NOT NULL,
        [PaymentLinkId] nvarchar(100) NULL,
        [CheckoutUrl] nvarchar(500) NULL,
        [QrCode] nvarchar(1000) NULL,
        [TransactionRef] nvarchar(100) NULL,
        [PaidAt] datetimeoffset NULL,
        [ExpiredAt] datetimeoffset NULL,
        [FailureReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_PaymentOrder] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentOrder_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallet] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [PayoutRequest] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [BankAccountId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [PayoutNote] nvarchar(500) NULL,
        [ProcessedBy] uniqueidentifier NULL,
        [ProcessedAt] datetimeoffset NULL,
        [TransactionRef] nvarchar(100) NULL,
        [RejectReason] nvarchar(500) NULL,
        [BankAccountSnapshot] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_PayoutRequest] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutRequest_BankAccount_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BankAccount] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayoutRequest_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayoutRequest_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallet] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE TABLE [WalletTransaction] (
        [Id] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [Type] nvarchar(30) NOT NULL,
        [Direction] nvarchar(3) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [RefType] nvarchar(30) NULL,
        [RefId] uniqueidentifier NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_WalletTransaction] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WalletTransaction_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallet] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_BankAccount_UserId_IsDeleted] ON [BankAccount] ([UserId], [IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_BankAccount_DefaultPerUser] ON [BankAccount] ([UserId]) WHERE [IsDefault] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PaymentOrder_Status] ON [PaymentOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PaymentOrder_UserId_CreatedAt] ON [PaymentOrder] ([UserId], [CreatedAt] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PaymentOrder_WalletId] ON [PaymentOrder] ([WalletId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentOrder_Gateway_PayOsOrderCode] ON [PaymentOrder] ([Gateway], [PayOsOrderCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PaymentOrder_Gateway_TransactionRef] ON [PaymentOrder] ([Gateway], [TransactionRef]) WHERE [TransactionRef] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentOrder_OrderRef] ON [PaymentOrder] ([OrderRef]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PayoutRequest_BankAccountId] ON [PayoutRequest] ([BankAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PayoutRequest_Status] ON [PayoutRequest] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PayoutRequest_UserId_CreatedAt] ON [PayoutRequest] ([UserId], [CreatedAt] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_PayoutRequest_WalletId] ON [PayoutRequest] ([WalletId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PlatformConfig_Key] ON [PlatformConfig] ([Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Wallet_UserId] ON [Wallet] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    CREATE INDEX [IX_WalletTransaction_WalletId_CreatedAt] ON [WalletTransaction] ([WalletId], [CreatedAt] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WalletTransaction_Ref] ON [WalletTransaction] ([RefType], [RefId], [Type]) WHERE [RefId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915055358_AddPaymentWalletCore'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260915055358_AddPaymentWalletCore', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915145531_AddWalletLockedBalanceAfter'
)
BEGIN
    ALTER TABLE [WalletTransaction] ADD [LockedBalanceAfter] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915145531_AddWalletLockedBalanceAfter'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260915145531_AddWalletLockedBalanceAfter', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Commissions] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [VoucherId] uniqueidentifier NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TotalPrice] decimal(18,2) NOT NULL,
        [FinalPrice] decimal(18,2) NOT NULL,
        [EscrowHeldAmount] decimal(18,2) NOT NULL,
        [DisbursedAmount] decimal(18,2) NOT NULL,
        [EscrowStatus] nvarchar(50) NOT NULL,
        [CurrentStage] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [DeadlineAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Commissions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [CreatorProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [Headline] nvarchar(200) NULL,
        [Bio] nvarchar(max) NULL,
        [Specialties] nvarchar(500) NULL,
        [Location] nvarchar(200) NULL,
        [WebsiteUrl] nvarchar(500) NULL,
        [BannerUrl] nvarchar(500) NULL,
        [IsAcceptingOrders] bit NOT NULL,
        [IsApproved] bit NOT NULL,
        [RatingAverage] decimal(18,2) NOT NULL,
        [RatingCount] int NOT NULL,
        [FollowerCount] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Tags] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [IsAiGenerated] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Disputes] (
        [Id] uniqueidentifier NOT NULL,
        [CommissionId] uniqueidentifier NOT NULL,
        [RaisedById] uniqueidentifier NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [EvidenceUrls] nvarchar(max) NULL,
        [Status] nvarchar(50) NOT NULL,
        [Resolution] nvarchar(50) NULL,
        [ClientRefundAmount] decimal(18,2) NULL,
        [ArtistPayAmount] decimal(18,2) NULL,
        [AdminNote] nvarchar(max) NULL,
        [ResolvedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Disputes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Disputes_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Milestones] (
        [Id] uniqueidentifier NOT NULL,
        [CommissionId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [WipPreviewUrl] nvarchar(500) NULL,
        [WatermarkedUrl] nvarchar(500) NULL,
        [FinalDeliverableUrl] nvarchar(500) NULL,
        [RevisionCount] int NOT NULL,
        [SubmittedAt] datetimeoffset NULL,
        [ApprovedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Milestones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Milestones_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [CommissionId] uniqueidentifier NOT NULL,
        [ReviewerId] uniqueidentifier NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NULL,
        [ReviewerReply] nvarchar(1000) NULL,
        [IsVisible] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Artworks] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [IsAiGenerated] bit NOT NULL,
        [AiDetectionScore] decimal(18,2) NULL,
        [ModerationStatus] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [ViewCount] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Artworks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Artworks_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [Follows] (
        [FollowerUserId] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Follows] PRIMARY KEY ([FollowerUserId], [CreatorProfileId]),
        CONSTRAINT [FK_Follows_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Follows_Users_FollowerUserId] FOREIGN KEY ([FollowerUserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE TABLE [ArtworkTags] (
        [ArtworkId] uniqueidentifier NOT NULL,
        [TagId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ArtworkTags] PRIMARY KEY ([ArtworkId], [TagId]),
        CONSTRAINT [FK_ArtworkTags_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ArtworkTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_Artworks_CreatorProfileId] ON [Artworks] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_Artworks_ModerationStatus] ON [Artworks] ([ModerationStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_ArtworkTags_TagId] ON [ArtworkTags] ([TagId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CreatorProfiles_UserId] ON [CreatorProfiles] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_Disputes_CommissionId] ON [Disputes] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_Follows_CreatorProfileId] ON [Follows] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_Milestones_CommissionId] ON [Milestones] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE INDEX [IX_Reviews_CommissionId] ON [Reviews] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tags_Name] ON [Tags] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085937_AddArtistStudioAndCommissionSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918085937_AddArtistStudioAndCommissionSchema', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE TABLE [Notification] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [NotificationType] nvarchar(50) NOT NULL,
        [NotificationTitle] nvarchar(200) NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [RefType] nvarchar(30) NULL,
        [RefId] uniqueidentifier NULL,
        [Channel] nvarchar(20) NOT NULL DEFAULT N'InApp',
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ReadAt] datetimeoffset NULL,
        [DedupKey] nvarchar(200) NULL,
        [SentAt] datetimeoffset NULL,
        [FailedReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Notification] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notification_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE TABLE [Voucher] (
        [Id] uniqueidentifier NOT NULL,
        [VoucherCode] nvarchar(50) NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [Name] nvarchar(200) NULL,
        [DiscountType] nvarchar(20) NOT NULL,
        [DiscountValue] decimal(18,2) NOT NULL,
        [MinOrderAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [MaxDiscountAmount] decimal(18,2) NULL,
        [Scope] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [UsageLimit] int NULL,
        [UsedCount] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Voucher] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Voucher_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE TABLE [VoucherRedemption] (
        [Id] uniqueidentifier NOT NULL,
        [VoucherId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RefType] nvarchar(30) NOT NULL,
        [RefId] uniqueidentifier NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [OrderAmount] decimal(18,2) NOT NULL,
        [FinalAmount] decimal(18,2) NOT NULL,
        [RedeemedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_VoucherRedemption] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VoucherRedemption_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VoucherRedemption_Voucher_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Voucher] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_Notification_UserId_CreatedAt] ON [Notification] ([UserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_Notification_UserId_IsRead] ON [Notification] ([UserId], [IsRead]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Notification_UserId_DedupKey] ON [Notification] ([UserId], [DedupKey]) WHERE [DedupKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_Voucher_Active_Window] ON [Voucher] ([IsActive], [StartDate], [EndDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_Voucher_CreatedByUserId] ON [Voucher] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_Voucher_IsDeleted] ON [Voucher] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Voucher_VoucherCode] ON [Voucher] ([VoucherCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_VoucherRedemption_UserId] ON [VoucherRedemption] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE INDEX [IX_VoucherRedemption_Voucher_User] ON [VoucherRedemption] ([VoucherId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VoucherRedemption_Ref] ON [VoucherRedemption] ([RefType], [RefId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918085959_AddVoucherAndNotification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918085959_AddVoucherAndNotification', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918122622_AddArtistStudio'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918122622_AddArtistStudio', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918123000_AddArtistStudioFix'
)
BEGIN
    ALTER TABLE [Follows] DROP CONSTRAINT [FK_Follows_Users_FollowerUserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918123000_AddArtistStudioFix'
)
BEGIN
    ALTER TABLE [Follows] ADD CONSTRAINT [FK_Follows_Users_FollowerUserId] FOREIGN KEY ([FollowerUserId]) REFERENCES [Users] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918123000_AddArtistStudioFix'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918123000_AddArtistStudioFix', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919160306_UniqueCommissionReviewAndDispute'
)
BEGIN
    DROP INDEX [IX_Reviews_CommissionId] ON [Reviews];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919160306_UniqueCommissionReviewAndDispute'
)
BEGIN
    DROP INDEX [IX_Disputes_CommissionId] ON [Disputes];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919160306_UniqueCommissionReviewAndDispute'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reviews_CommissionId] ON [Reviews] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919160306_UniqueCommissionReviewAndDispute'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Disputes_CommissionId] ON [Disputes] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919160306_UniqueCommissionReviewAndDispute'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260919160306_UniqueCommissionReviewAndDispute', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920124410_AddCreatorApplicationsTable'
)
BEGIN
    CREATE TABLE [CreatorApplications] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicantId] uniqueidentifier NOT NULL,
        [PortfolioLinks] nvarchar(max) NOT NULL,
        [SocialLinks] nvarchar(max) NULL,
        [IdProofUrl] nvarchar(500) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [ReviewedByModId] uniqueidentifier NULL,
        [ReviewNote] nvarchar(max) NULL,
        [SubmittedAt] datetimeoffset NOT NULL,
        [ReviewedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorApplications_Users_ApplicantId] FOREIGN KEY ([ApplicantId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CreatorApplications_Users_ReviewedByModId] FOREIGN KEY ([ReviewedByModId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920124410_AddCreatorApplicationsTable'
)
BEGIN
    CREATE INDEX [IX_CreatorApplications_ApplicantId_Status] ON [CreatorApplications] ([ApplicantId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920124410_AddCreatorApplicationsTable'
)
BEGIN
    CREATE INDEX [IX_CreatorApplications_ReviewedByModId] ON [CreatorApplications] ([ReviewedByModId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920124410_AddCreatorApplicationsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260920124410_AddCreatorApplicationsTable', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [AdultScore] decimal(5,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [FileSizeBytes] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [FlagReason] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [ModeratedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [ModerationNote] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [ModeratorId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [Resolution] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [SafeScore] decimal(5,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    ALTER TABLE [Artworks] ADD [ViolenceScore] decimal(5,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920152747_AddArtworkModerationFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260920152747_AddArtworkModerationFields', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920154858_AddCreatorApplicationAndAiVerifiedFields'
)
BEGIN
    ALTER TABLE [CreatorProfiles] ADD [IsAiVerified] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920154858_AddCreatorApplicationAndAiVerifiedFields'
)
BEGIN
    ALTER TABLE [CreatorApplications] ADD [IsNationalIdVerified] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920154858_AddCreatorApplicationAndAiVerifiedFields'
)
BEGIN
    ALTER TABLE [CreatorApplications] ADD [PrimaryStyle] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920154858_AddCreatorApplicationAndAiVerifiedFields'
)
BEGIN
    ALTER TABLE [CreatorApplications] ADD [SpeedpaintVideoUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920154858_AddCreatorApplicationAndAiVerifiedFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260920154858_AddCreatorApplicationAndAiVerifiedFields', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.PlatformConfig', 'UpdatedByAdminId') IS NULL ALTER TABLE [dbo].[PlatformConfig] ADD [UpdatedByAdminId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.CreatorProfiles', 'CommissionSlots') IS NULL ALTER TABLE [dbo].[CreatorProfiles] ADD [CommissionSlots] int NOT NULL CONSTRAINT [DF_CreatorProfiles_CommissionSlots] DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.CreatorProfiles', 'CompletedOrdersCount') IS NULL ALTER TABLE [dbo].[CreatorProfiles] ADD [CompletedOrdersCount] int NOT NULL CONSTRAINT [DF_CreatorProfiles_CompletedOrdersCount] DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.CreatorProfiles', 'RateCard') IS NULL ALTER TABLE [dbo].[CreatorProfiles] ADD [RateCard] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.Artworks', 'LikeCount') IS NULL ALTER TABLE [dbo].[Artworks] ADD [LikeCount] int NOT NULL CONSTRAINT [DF_Artworks_LikeCount] DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.Artworks', 'Style') IS NULL ALTER TABLE [dbo].[Artworks] ADD [Style] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    IF COL_LENGTH('dbo.Artworks', 'WatermarkedUrl') IS NULL ALTER TABLE [dbo].[Artworks] ADD [WatermarkedUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [AiConversations] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Topic] nvarchar(300) NOT NULL,
        [ContextType] nvarchar(20) NOT NULL DEFAULT N'General',
        [ContextId] uniqueidentifier NULL,
        [LastMessageAt] datetimeoffset NULL,
        [MessageCount] int NOT NULL DEFAULT 0,
        [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_AiConversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiConversations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [Auctions] (
        [Id] uniqueidentifier NOT NULL,
        [ArtworkId] uniqueidentifier NOT NULL,
        [SellerId] uniqueidentifier NOT NULL,
        [AuctionType] nvarchar(20) NOT NULL DEFAULT N'Standard',
        [StartPrice] decimal(18,2) NOT NULL,
        [ReservePrice] decimal(18,2) NULL,
        [BidStep] decimal(18,2) NOT NULL,
        [BuyNowPrice] decimal(18,2) NULL,
        [CurrentPrice] decimal(18,2) NOT NULL,
        [BidCount] int NOT NULL DEFAULT 0,
        [StartAt] datetimeoffset NOT NULL,
        [EndAt] datetimeoffset NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Scheduled',
        [WinnerId] uniqueidentifier NULL,
        [FinalPrice] decimal(18,2) NULL,
        [SettledAt] datetimeoffset NULL,
        [PaymentDeadline] datetimeoffset NULL,
        [CancelReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Auctions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Auctions_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Auctions_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Auctions_Users_WinnerId] FOREIGN KEY ([WinnerId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [DeadlineReminders] (
        [Id] uniqueidentifier NOT NULL,
        [CommissionId] uniqueidentifier NOT NULL,
        [MilestoneId] uniqueidentifier NULL,
        [RemindAt] datetimeoffset NOT NULL,
        [Channel] nvarchar(20) NOT NULL DEFAULT N'InApp',
        [SentAt] datetimeoffset NULL,
        [RiskScore] decimal(5,2) NOT NULL,
        [PredictedAt] datetimeoffset NULL,
        [ModelVersion] nvarchar(80) NULL,
        [FailureReason] nvarchar(500) NULL,
        [IsManual] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_DeadlineReminders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeadlineReminders_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DeadlineReminders_Milestones_MilestoneId] FOREIGN KEY ([MilestoneId]) REFERENCES [Milestones] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [RevenueSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [Scope] nvarchar(20) NOT NULL DEFAULT N'Daily',
        [SnapshotDate] date NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [FeeAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [CompletedOrderCount] int NOT NULL DEFAULT 0,
        [CommissionGrossAmount] decimal(18,2) NULL,
        [AuctionGrossAmount] decimal(18,2) NULL,
        [RebuiltAt] datetimeoffset NULL,
        [RebuildCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_RevenueSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RevenueSnapshots_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [UserReminderSettings] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [EmailEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [PushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LeadHours] int NOT NULL DEFAULT 24,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_UserReminderSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserReminderSettings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [UserSanctions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ActionType] nvarchar(50) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [DurationDays] int NULL,
        [ExpiresAt] datetimeoffset NULL,
        [ActionByAdminId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_UserSanctions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserSanctions_Users_ActionByAdminId] FOREIGN KEY ([ActionByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserSanctions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [AiMessages] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL DEFAULT N'User',
        [Content] nvarchar(max) NOT NULL,
        [TokenCount] int NULL,
        [FeedbackHelpful] bit NULL,
        [FeedbackNote] nvarchar(1000) NULL,
        [FeedbackAt] datetimeoffset NULL,
        [ModelVersion] nvarchar(80) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_AiMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiMessages_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [ArtworkOwnerships] (
        [Id] uniqueidentifier NOT NULL,
        [ArtworkId] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [AcquiredAt] datetimeoffset NOT NULL,
        [ReleasedAt] datetimeoffset NULL,
        [TransferReason] nvarchar(30) NOT NULL DEFAULT N'AuctionWin',
        [AuctionId] uniqueidentifier NULL,
        [CommissionId] uniqueidentifier NULL,
        [IsCurrent] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ArtworkOwnerships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArtworkOwnerships_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ArtworkOwnerships_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ArtworkOwnerships_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [AuctionWatches] (
        [Id] uniqueidentifier NOT NULL,
        [AuctionId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [NotifyOnOutbid] bit NOT NULL DEFAULT CAST(1 AS bit),
        [NotifyEndingSoon] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_AuctionWatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuctionWatches_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AuctionWatches_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [Bids] (
        [Id] uniqueidentifier NOT NULL,
        [AuctionId] uniqueidentifier NOT NULL,
        [BidderId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [HoldAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Leading',
        [HoldStatus] nvarchar(20) NOT NULL DEFAULT N'None',
        [IsAuto] bit NOT NULL,
        [MaxAutoBid] decimal(18,2) NULL,
        [PlacedAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Bids] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bids_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Bids_Users_BidderId] FOREIGN KEY ([BidderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [ChatRooms] (
        [Id] uniqueidentifier NOT NULL,
        [RoomType] nvarchar(20) NOT NULL DEFAULT N'Commission',
        [Title] nvarchar(200) NOT NULL,
        [CommissionId] uniqueidentifier NULL,
        [AuctionId] uniqueidentifier NULL,
        [LastMessageAt] datetimeoffset NULL,
        [IsLocked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ChatRooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatRooms_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ChatRooms_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [Deliverables] (
        [Id] uniqueidentifier NOT NULL,
        [AuctionId] uniqueidentifier NOT NULL,
        [RecipientId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(300) NOT NULL,
        [FileUrl] nvarchar(1000) NOT NULL,
        [MimeType] nvarchar(120) NULL,
        [FileSizeBytes] bigint NULL,
        [IsDelivered] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeliveredAt] datetimeoffset NULL,
        [DownloadCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Deliverables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Deliverables_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Deliverables_Users_RecipientId] FOREIGN KEY ([RecipientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [EscrowTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [AuctionId] uniqueidentifier NOT NULL,
        [PayerId] uniqueidentifier NOT NULL,
        [PayeeId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReleasedAmount] decimal(18,2) NOT NULL,
        [RefundedAmount] decimal(18,2) NOT NULL,
        [FeeAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ReleasedAt] datetimeoffset NULL,
        [RefundedAt] datetimeoffset NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_EscrowTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EscrowTransactions_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EscrowTransactions_Users_PayeeId] FOREIGN KEY ([PayeeId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EscrowTransactions_Users_PayerId] FOREIGN KEY ([PayerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [ChatRoomMembers] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [MemberRole] nvarchar(30) NOT NULL,
        [LastReadAt] datetimeoffset NULL,
        [HasLeft] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ChatRoomMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatRoomMembers_ChatRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [ChatRooms] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChatRoomMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NULL,
        [CommissionId] uniqueidentifier NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [MessageType] nvarchar(50) NOT NULL DEFAULT N'Text',
        [Body] nvarchar(max) NOT NULL,
        [AttachmentUrl] nvarchar(500) NULL,
        [SourceLang] nvarchar(10) NULL,
        [TargetLang] nvarchar(10) NULL,
        [TranslatedBody] nvarchar(max) NULL,
        [TranslationStatus] nvarchar(20) NOT NULL DEFAULT N'NotRequested',
        [TranslationError] nvarchar(500) NULL,
        [SentAt] datetimeoffset NOT NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_ChatRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [ChatRooms] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Messages_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE TABLE [MessageAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [MessageId] uniqueidentifier NOT NULL,
        [UploadedBy] uniqueidentifier NOT NULL,
        [FileUrl] nvarchar(1000) NOT NULL,
        [FileName] nvarchar(300) NULL,
        [MimeType] nvarchar(120) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_MessageAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageAttachments_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [Messages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MessageAttachments_Users_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_AiConversations_Context] ON [AiConversations] ([ContextType], [ContextId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_AiConversations_User_LastMessageAt] ON [AiConversations] ([UserId], [LastMessageAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_AiMessages_Conversation_CreatedAt] ON [AiMessages] ([ConversationId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_AiMessages_FeedbackHelpful] ON [AiMessages] ([FeedbackHelpful]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_ArtworkOwnerships_AuctionId] ON [ArtworkOwnerships] ([AuctionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_ArtworkOwnerships_Owner_Current] ON [ArtworkOwnerships] ([OwnerId], [IsCurrent]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ArtworkOwnerships_CurrentOwner] ON [ArtworkOwnerships] ([ArtworkId]) WHERE [IsCurrent] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Auctions_ArtworkId] ON [Auctions] ([ArtworkId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Auctions_Seller_Status] ON [Auctions] ([SellerId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Auctions_Status_CurrentPrice] ON [Auctions] ([Status], [CurrentPrice]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Auctions_Status_EndAt] ON [Auctions] ([Status], [EndAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Auctions_WinnerId] ON [Auctions] ([WinnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_AuctionWatches_UserId] ON [AuctionWatches] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AuctionWatches_Auction_User] ON [AuctionWatches] ([AuctionId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Bids_Auction_PlacedAt] ON [Bids] ([AuctionId], [PlacedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Bids_BidderId] ON [Bids] ([BidderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Bids_OneLeadingPerAuction] ON [Bids] ([AuctionId]) WHERE [Status] = ''Leading''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_ChatRoomMembers_UserId] ON [ChatRoomMembers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ChatRoomMembers_Room_User] ON [ChatRoomMembers] ([RoomId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_AuctionId] ON [ChatRooms] ([AuctionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_CommissionId] ON [ChatRooms] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_LastMessageAt] ON [ChatRooms] ([LastMessageAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_DeadlineReminders_CommissionId] ON [DeadlineReminders] ([CommissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_DeadlineReminders_MilestoneId] ON [DeadlineReminders] ([MilestoneId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_DeadlineReminders_Pending_RemindAt] ON [DeadlineReminders] ([SentAt], [RemindAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Deliverables_RecipientId] ON [Deliverables] ([RecipientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Deliverables_Auction] ON [Deliverables] ([AuctionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_EscrowTransactions_PayeeId] ON [EscrowTransactions] ([PayeeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_EscrowTransactions_PayerId] ON [EscrowTransactions] ([PayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EscrowTransactions_Auction] ON [EscrowTransactions] ([AuctionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_MessageAttachments_MessageId] ON [MessageAttachments] ([MessageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_MessageAttachments_UploadedBy] ON [MessageAttachments] ([UploadedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Messages_Commission_SentAt] ON [Messages] ([CommissionId], [SentAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Messages_Room_SentAt] ON [Messages] ([RoomId], [SentAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_Messages_SenderId] ON [Messages] ([SenderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_RevenueSnapshots_Scope_Date] ON [RevenueSnapshots] ([Scope], [SnapshotDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RevenueSnapshots_Creator_Scope_Date] ON [RevenueSnapshots] ([CreatorId], [Scope], [SnapshotDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_UserReminderSettings_UserId] ON [UserReminderSettings] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_UserSanctions_ActionByAdminId] ON [UserSanctions] ([ActionByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    CREATE INDEX [IX_UserSanctions_UserId_CreatedAt] ON [UserSanctions] ([UserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921090850_AddAuctionChatAiAndRevenueSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921090850_AddAuctionChatAiAndRevenueSchema', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    ALTER TABLE [CreatorProfiles] ADD [AvailableSlots] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    ALTER TABLE [Artworks] ADD [LicenseType] nvarchar(30) NOT NULL DEFAULT N'Personal';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    ALTER TABLE [Artworks] ADD [StartingPrice] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    IF COL_LENGTH('dbo.Artworks', 'Style') IS NULL ALTER TABLE [dbo].[Artworks] ADD [Style] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE TABLE [ArtworkComments] (
        [Id] uniqueidentifier NOT NULL,
        [ArtworkId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ArtworkComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArtworkComments_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE TABLE [ArtworkFavorites] (
        [UserId] uniqueidentifier NOT NULL,
        [ArtworkId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ArtworkFavorites] PRIMARY KEY ([UserId], [ArtworkId]),
        CONSTRAINT [FK_ArtworkFavorites_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE TABLE [CommissionServices] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(max) NULL,
        [StartingPrice] decimal(18,2) NOT NULL,
        [DeliveryDays] int NOT NULL,
        [MaxRevisions] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CommissionServices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommissionServices_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE TABLE [CreatorReviews] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [ReviewerUserId] uniqueidentifier NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorReviews_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE TABLE [PersonalCollections] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsPublic] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_PersonalCollections] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE TABLE [CollectionArtworks] (
        [CollectionId] uniqueidentifier NOT NULL,
        [ArtworkId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CollectionArtworks] PRIMARY KEY ([CollectionId], [ArtworkId]),
        CONSTRAINT [FK_CollectionArtworks_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]),
        CONSTRAINT [FK_CollectionArtworks_PersonalCollections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [PersonalCollections] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE INDEX [IX_ArtworkComments_ArtworkId_CreatedAt] ON [ArtworkComments] ([ArtworkId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE INDEX [IX_ArtworkFavorites_ArtworkId] ON [ArtworkFavorites] ([ArtworkId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE INDEX [IX_CollectionArtworks_ArtworkId] ON [CollectionArtworks] ([ArtworkId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE INDEX [IX_CommissionServices_CreatorProfileId_IsActive] ON [CommissionServices] ([CreatorProfileId], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE INDEX [IX_CreatorReviews_CreatorProfileId_CreatedAt] ON [CreatorReviews] ([CreatorProfileId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    CREATE INDEX [IX_PersonalCollections_OwnerUserId_CreatedAt] ON [PersonalCollections] ([OwnerUserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921175834_AddMarketplaceDiscoveryInteractions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921175834_AddMarketplaceDiscoveryInteractions', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    IF COL_LENGTH(N'[CreatorProfiles]', N'RateCard') IS NOT NULL
        ALTER TABLE [CreatorProfiles] DROP COLUMN [RateCard];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    ALTER TABLE [Reviews] ADD [AttachedImagesJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    ALTER TABLE [Reviews] ADD [RespondedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    ALTER TABLE [Milestones] ADD [CreatorNote] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    ALTER TABLE [Milestones] ADD [MaxRevisions] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    ALTER TABLE [Milestones] ADD [OriginalWipUrl] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    ALTER TABLE [Milestones] ADD [RevisionFeedback] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921182149_AddWorkroomMilestoneFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921182149_AddWorkroomMilestoneFields', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922120000_AddCreatorRateCardJson'
)
BEGIN
    ALTER TABLE [CreatorProfiles] ADD [RateCardJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922120000_AddCreatorRateCardJson'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922120000_AddCreatorRateCardJson', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE TABLE [CreatorAssets] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [AssetType] nvarchar(max) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [AssetUrl] nvarchar(max) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorAssets_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE TABLE [CreatorAutoReplySettings] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [IsEnabled] bit NOT NULL,
        [BriefTemplate] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorAutoReplySettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorAutoReplySettings_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE TABLE [CreatorFaqs] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [Question] nvarchar(max) NOT NULL,
        [Answer] nvarchar(max) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorFaqs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorFaqs_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE TABLE [CreatorTerms] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [CommercialLicenseMultiplier] decimal(18,2) NOT NULL,
        [RevisionPolicy] nvarchar(max) NULL,
        [CancellationPolicy] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorTerms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorTerms_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE TABLE [CreatorWorkItems] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorProfileId] uniqueidentifier NOT NULL,
        [ClientName] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Stage] nvarchar(max) NOT NULL,
        [DueAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CreatorWorkItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreatorWorkItems_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE INDEX [IX_CreatorAssets_CreatorProfileId] ON [CreatorAssets] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE INDEX [IX_CreatorAutoReplySettings_CreatorProfileId] ON [CreatorAutoReplySettings] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE INDEX [IX_CreatorFaqs_CreatorProfileId] ON [CreatorFaqs] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE INDEX [IX_CreatorTerms_CreatorProfileId] ON [CreatorTerms] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    CREATE INDEX [IX_CreatorWorkItems_CreatorProfileId] ON [CreatorWorkItems] ([CreatorProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927132120_SyncLocalRuntimeSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927132120_SyncLocalRuntimeSchema', N'8.0.8');
END;
GO

COMMIT;
GO
