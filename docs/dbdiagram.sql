CREATE TABLE [Commissions] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [ClientId] uniqueidentifier NOT NULL,
    [CreatorId] uniqueidentifier NOT NULL,
    [VoucherId] uniqueidentifier NULL,
    [LicenseType] nvarchar(20) NOT NULL,
    [LicenseMultiplierApplied] decimal(5,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [TotalPrice] decimal(18,2) NOT NULL,
    [FinalPrice] decimal(18,2) NOT NULL,
    [EscrowHeldAmount] decimal(18,2) NOT NULL,
    [DisbursedAmount] decimal(18,2) NOT NULL,
    [EscrowStatus] nvarchar(50) NOT NULL,
    [CurrentStage] int NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [DeadlineAt] datetimeoffset NULL,
    [CancellationReason] nvarchar(500) NULL,
    [CancellationDetails] nvarchar(2000) NULL,
    [CancellationRequestedById] uniqueidentifier NULL,
    [CancellationRequestedByRole] nvarchar(20) NULL,
    [CancellationRequestStatus] nvarchar(20) NULL,
    [CancellationRequestedAt] datetimeoffset NULL,
    [CancellationRequestExpiresAt] datetimeoffset NULL,
    [ClientTermsAcceptedAt] datetimeoffset NULL,
    [ClientTermsVersion] nvarchar(20) NULL,
    [CreatorTermsAcceptedAt] datetimeoffset NULL,
    [CreatorTermsVersion] nvarchar(20) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Commissions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [EmailVerificationCodes] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Email] nvarchar(256) NOT NULL,
    [CodeHash] nvarchar(64) NOT NULL,
    [Purpose] nvarchar(30) NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [AttemptCount] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_EmailVerificationCodes] PRIMARY KEY ([Id])
);
GO


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
GO


CREATE TABLE [PlatformConfig] (
    [Id] uniqueidentifier NOT NULL,
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(500) NOT NULL,
    [Description] nvarchar(500) NULL,
    [UpdatedByAdminId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_PlatformConfig] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Tags] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [IsAiGenerated] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [FullName] nvarchar(150) NOT NULL,
    [IsVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
    [LoginUsername] nvarchar(30) NULL,
    [NormalizedLoginUsername] nvarchar(30) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [DeletedAt] datetimeoffset NULL,
    [AvatarUrl] nvarchar(500) NULL,
    [CoverUrl] nvarchar(500) NULL,
    [Bio] nvarchar(1000) NULL,
    [SocialLinks] nvarchar(max) NOT NULL,
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
GO


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
GO


CREATE TABLE [Milestones] (
    [Id] uniqueidentifier NOT NULL,
    [CommissionId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [OriginalWipUrl] nvarchar(max) NULL,
    [WipPreviewUrl] nvarchar(500) NULL,
    [WatermarkedUrl] nvarchar(500) NULL,
    [FinalDeliverableUrl] nvarchar(500) NULL,
    [RevisionCount] int NOT NULL,
    [MaxRevisions] int NOT NULL,
    [CreatorNote] nvarchar(max) NULL,
    [RevisionFeedback] nvarchar(max) NULL,
    [SubmittedAt] datetimeoffset NULL,
    [ApprovedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Milestones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Milestones_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Reviews] (
    [Id] uniqueidentifier NOT NULL,
    [CommissionId] uniqueidentifier NOT NULL,
    [ReviewerId] uniqueidentifier NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(1000) NULL,
    [ReviewerReply] nvarchar(1000) NULL,
    [IsVisible] bit NOT NULL,
    [AttachedImagesJson] nvarchar(max) NULL,
    [RespondedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reviews_Commissions_CommissionId] FOREIGN KEY ([CommissionId]) REFERENCES [Commissions] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [RoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);
GO


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
GO


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
GO


CREATE TABLE [ClientProfiles] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Username] nvarchar(30) NULL,
    [UsernameChangedAt] datetimeoffset NULL,
    [InterestTags] nvarchar(max) NOT NULL,
    [Country] nvarchar(100) NULL,
    [Timezone] nvarchar(100) NULL,
    [PreferredLanguages] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_ClientProfiles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ClientReviews] (
    [Id] uniqueidentifier NOT NULL,
    [CommissionId] uniqueidentifier NOT NULL,
    [CreatorId] uniqueidentifier NOT NULL,
    [ClientId] uniqueidentifier NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(1000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_ClientReviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClientReviews_Users_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ClientReviews_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id])
);
GO


CREATE TABLE [CreatorApplications] (
    [Id] uniqueidentifier NOT NULL,
    [ApplicantId] uniqueidentifier NOT NULL,
    [PortfolioLinks] nvarchar(max) NOT NULL,
    [SocialLinks] nvarchar(max) NULL,
    [IdProofUrl] nvarchar(500) NOT NULL,
    [IdProofBackUrl] nvarchar(500) NOT NULL,
    [PrimaryStyle] nvarchar(100) NULL,
    [SpeedpaintVideoUrls] nvarchar(max) NOT NULL,
    [IsNationalIdVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
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
GO


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
    [CommissionSlots] int NOT NULL DEFAULT 0,
    [CompletedOrdersCount] int NOT NULL DEFAULT 0,
    [RateCardJson] nvarchar(max) NULL,
    [IsApproved] bit NOT NULL,
    [IsAiVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RatingAverage] decimal(18,2) NOT NULL,
    [RatingCount] int NOT NULL,
    [FollowerCount] int NOT NULL,
    [AvailableSlots] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CreatorProfiles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreatorProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


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
GO


CREATE TABLE [PlatformEvents] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [BannerUrl] nvarchar(500) NULL,
    [Description] nvarchar(max) NOT NULL,
    [Rules] nvarchar(max) NULL,
    [Prize] nvarchar(1000) NULL,
    [MaxVote] int NOT NULL DEFAULT 1,
    [Status] nvarchar(50) NOT NULL DEFAULT N'Draft',
    [IsFeatured] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsHeadJurySignedOff] bit NOT NULL DEFAULT CAST(0 AS bit),
    [HeadJurySignedOffAt] datetimeoffset NULL,
    [SubmissionStartAt] datetimeoffset NOT NULL,
    [SubmissionEndAt] datetimeoffset NOT NULL,
    [JudgingStartAt] datetimeoffset NOT NULL,
    [JudgingEndAt] datetimeoffset NOT NULL,
    [VotingStartAt] datetimeoffset NOT NULL,
    [VotingEndAt] datetimeoffset NOT NULL,
    [ResultAnnouncementAt] datetimeoffset NOT NULL,
    [CreatedByAdminId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_PlatformEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PlatformEvents_Timeline] CHECK ([SubmissionStartAt] < [SubmissionEndAt] AND [SubmissionEndAt] < [JudgingStartAt] AND [JudgingStartAt] < [JudgingEndAt] AND [JudgingEndAt] < [VotingStartAt] AND [VotingStartAt] < [VotingEndAt] AND [VotingEndAt] < [ResultAnnouncementAt]),
    CONSTRAINT [FK_PlatformEvents_Users_CreatedByAdminId] FOREIGN KEY ([CreatedByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(450) NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [RevokedAt] datetimeoffset NULL,
    [CreatedByIp] nvarchar(50) NULL,
    [ReplacedByTokenHash] nvarchar(450) NULL,
    [UserAgent] nvarchar(max) NULL,
    [LastUsedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


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
GO


CREATE TABLE [TwoFactorRecoveryCodes] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [CodeHash] nvarchar(450) NOT NULL,
    [IsUsed] bit NOT NULL,
    [UsedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_TwoFactorRecoveryCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TwoFactorRecoveryCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [UserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [UserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


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
GO


CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


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
GO


CREATE TABLE [UserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


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
GO


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
GO


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
GO


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
GO


CREATE TABLE [Artworks] (
    [Id] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [ImageUrl] nvarchar(500) NOT NULL,
    [ThumbnailUrl] nvarchar(500) NULL,
    [WatermarkedUrl] nvarchar(500) NULL,
    [Style] nvarchar(100) NULL,
    [LikeCount] int NOT NULL DEFAULT 0,
    [IsAiGenerated] bit NOT NULL,
    [AiDetectionScore] decimal(18,2) NULL,
    [ModerationStatus] nvarchar(20) NOT NULL DEFAULT N'Pending',
    [ViewCount] int NOT NULL,
    [LicenseType] nvarchar(max) NOT NULL,
    [StartingPrice] decimal(18,2) NULL,
    [SafeScore] decimal(5,4) NULL,
    [AdultScore] decimal(5,4) NULL,
    [ViolenceScore] decimal(5,4) NULL,
    [FlagReason] nvarchar(200) NULL,
    [Resolution] nvarchar(50) NULL,
    [FileSizeBytes] bigint NULL,
    [ModeratorId] uniqueidentifier NULL,
    [ModerationNote] nvarchar(1000) NULL,
    [ModeratedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Artworks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Artworks_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
);
GO


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
GO


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
GO


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
GO


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
GO


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
GO


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
GO


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
GO


CREATE TABLE [Follows] (
    [FollowerUserId] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Follows] PRIMARY KEY ([FollowerUserId], [CreatorProfileId]),
    CONSTRAINT [FK_Follows_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Follows_Users_FollowerUserId] FOREIGN KEY ([FollowerUserId]) REFERENCES [Users] ([Id])
);
GO


CREATE TABLE [EventCriteria] (
    [Id] uniqueidentifier NOT NULL,
    [EventId] uniqueidentifier NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Description] nvarchar(500) NULL,
    [MaxScore] decimal(5,2) NOT NULL DEFAULT 10.0,
    [Weight] decimal(5,2) NOT NULL,
    [DisplayOrder] int NOT NULL DEFAULT 0,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_EventCriteria] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EventCriteria_PlatformEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [PlatformEvents] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Invitations] (
    [Id] uniqueidentifier NOT NULL,
    [EventId] uniqueidentifier NOT NULL,
    [SentFromAdminId] uniqueidentifier NOT NULL,
    [SentToCreatorId] uniqueidentifier NOT NULL,
    [IsHeadJury] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RespondedAt] datetimeoffset NULL,
    [Status] nvarchar(50) NOT NULL DEFAULT N'Pending',
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Invitations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invitations_CreatorProfiles_SentToCreatorId] FOREIGN KEY ([SentToCreatorId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Invitations_PlatformEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [PlatformEvents] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Invitations_Users_SentFromAdminId] FOREIGN KEY ([SentFromAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Juries] (
    [Id] uniqueidentifier NOT NULL,
    [EventId] uniqueidentifier NOT NULL,
    [CreatorId] uniqueidentifier NOT NULL,
    [IsHeadJury] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Juries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Juries_CreatorProfiles_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Juries_PlatformEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [PlatformEvents] ([Id]) ON DELETE CASCADE
);
GO


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
GO


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
GO


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
GO


CREATE TABLE [WalletTransaction] (
    [Id] uniqueidentifier NOT NULL,
    [WalletId] uniqueidentifier NOT NULL,
    [Type] nvarchar(30) NOT NULL,
    [Direction] nvarchar(3) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [BalanceAfter] decimal(18,2) NOT NULL,
    [LockedBalanceAfter] decimal(18,2) NOT NULL DEFAULT 0.0,
    [RefType] nvarchar(30) NULL,
    [RefId] uniqueidentifier NULL,
    [Note] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_WalletTransaction] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WalletTransaction_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallet] ([Id]) ON DELETE CASCADE
);
GO


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
GO


CREATE TABLE [ArtworkFavorites] (
    [UserId] uniqueidentifier NOT NULL,
    [ArtworkId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ArtworkFavorites] PRIMARY KEY ([UserId], [ArtworkId]),
    CONSTRAINT [FK_ArtworkFavorites_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ArtworkTags] (
    [ArtworkId] uniqueidentifier NOT NULL,
    [TagId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ArtworkTags] PRIMARY KEY ([ArtworkId], [TagId]),
    CONSTRAINT [FK_ArtworkTags_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ArtworkTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
);
GO


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
    [RowVersion] rowversion NOT NULL,
    [CancelReason] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Auctions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Auctions_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Auctions_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Auctions_Users_WinnerId] FOREIGN KEY ([WinnerId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [CollectionArtworks] (
    [CollectionId] uniqueidentifier NOT NULL,
    [ArtworkId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CollectionArtworks] PRIMARY KEY ([CollectionId], [ArtworkId]),
    CONSTRAINT [FK_CollectionArtworks_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]),
    CONSTRAINT [FK_CollectionArtworks_PersonalCollections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [PersonalCollections] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EventSubmissions] (
    [Id] uniqueidentifier NOT NULL,
    [EventId] uniqueidentifier NOT NULL,
    [SubmitterId] uniqueidentifier NOT NULL,
    [ArtworkId] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [AiScanPassed] bit NOT NULL DEFAULT CAST(0 AS bit),
    [VoteCount] int NOT NULL DEFAULT 0,
    [Score] decimal(5,2) NULL,
    [AdminNote] nvarchar(1000) NULL,
    [SubmittedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EventSubmissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EventSubmissions_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EventSubmissions_PlatformEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [PlatformEvents] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EventSubmissions_Users_SubmitterId] FOREIGN KEY ([SubmitterId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


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
GO


CREATE TABLE [AuctionAutoBids] (
    [Id] uniqueidentifier NOT NULL,
    [AuctionId] uniqueidentifier NOT NULL,
    [BidderId] uniqueidentifier NOT NULL,
    [MaxAmount] decimal(18,2) NOT NULL,
    [RegisteredAt] datetimeoffset NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_AuctionAutoBids] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuctionAutoBids_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AuctionAutoBids_Users_BidderId] FOREIGN KEY ([BidderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


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
GO


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
GO


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
GO


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
GO


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
GO


CREATE TABLE [CriteriaScores] (
    [EventCriteriaId] uniqueidentifier NOT NULL,
    [SubmissionId] uniqueidentifier NOT NULL,
    [GradedByJuryId] uniqueidentifier NOT NULL,
    [Score] decimal(5,2) NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CriteriaScores] PRIMARY KEY ([EventCriteriaId], [SubmissionId], [GradedByJuryId]),
    CONSTRAINT [FK_CriteriaScores_EventCriteria_EventCriteriaId] FOREIGN KEY ([EventCriteriaId]) REFERENCES [EventCriteria] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CriteriaScores_EventSubmissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [EventSubmissions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CriteriaScores_Juries_GradedByJuryId] FOREIGN KEY ([GradedByJuryId]) REFERENCES [Juries] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EventVotes] (
    [SubmissionId] uniqueidentifier NOT NULL,
    [VoterId] uniqueidentifier NOT NULL,
    [VotedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EventVotes] PRIMARY KEY ([SubmissionId], [VoterId]),
    CONSTRAINT [FK_EventVotes_EventSubmissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [EventSubmissions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EventVotes_Users_VoterId] FOREIGN KEY ([VoterId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [SubmissionDisputes] (
    [Id] uniqueidentifier NOT NULL,
    [EventId] uniqueidentifier NOT NULL,
    [SubmissionId] uniqueidentifier NOT NULL,
    [RaisedById] uniqueidentifier NOT NULL,
    [Priority] nvarchar(50) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [EvidenceUrls] nvarchar(max) NULL,
    [Status] nvarchar(50) NOT NULL,
    [Resolution] nvarchar(50) NULL,
    [Note] nvarchar(max) NULL,
    [ResolvedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_SubmissionDisputes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubmissionDisputes_EventSubmissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [EventSubmissions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubmissionDisputes_PlatformEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [PlatformEvents] ([Id]) ON DELETE NO ACTION
);
GO


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
GO


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
GO


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
GO


CREATE INDEX [IX_AiConversations_Context] ON [AiConversations] ([ContextType], [ContextId]);
GO


CREATE INDEX [IX_AiConversations_User_LastMessageAt] ON [AiConversations] ([UserId], [LastMessageAt]);
GO


CREATE INDEX [IX_AiMessages_Conversation_CreatedAt] ON [AiMessages] ([ConversationId], [CreatedAt]);
GO


CREATE INDEX [IX_AiMessages_FeedbackHelpful] ON [AiMessages] ([FeedbackHelpful]);
GO


CREATE INDEX [IX_ArtworkComments_ArtworkId] ON [ArtworkComments] ([ArtworkId]);
GO


CREATE INDEX [IX_ArtworkFavorites_ArtworkId] ON [ArtworkFavorites] ([ArtworkId]);
GO


CREATE INDEX [IX_ArtworkOwnerships_AuctionId] ON [ArtworkOwnerships] ([AuctionId]);
GO


CREATE INDEX [IX_ArtworkOwnerships_Owner_Current] ON [ArtworkOwnerships] ([OwnerId], [IsCurrent]);
GO


CREATE UNIQUE INDEX [UX_ArtworkOwnerships_CurrentOwner] ON [ArtworkOwnerships] ([ArtworkId]) WHERE [IsCurrent] = 1;
GO


CREATE INDEX [IX_Artworks_CreatorProfileId] ON [Artworks] ([CreatorProfileId]);
GO


CREATE INDEX [IX_Artworks_ModerationStatus] ON [Artworks] ([ModerationStatus]);
GO


CREATE INDEX [IX_ArtworkTags_TagId] ON [ArtworkTags] ([TagId]);
GO


CREATE INDEX [IX_AuctionAutoBids_BidderId] ON [AuctionAutoBids] ([BidderId]);
GO


CREATE INDEX [IX_AuctionAutoBids_ProxyOrder] ON [AuctionAutoBids] ([AuctionId], [MaxAmount], [RegisteredAt]);
GO


CREATE UNIQUE INDEX [UX_AuctionAutoBids_Auction_Bidder] ON [AuctionAutoBids] ([AuctionId], [BidderId]);
GO


CREATE INDEX [IX_Auctions_ArtworkId] ON [Auctions] ([ArtworkId]);
GO


CREATE INDEX [IX_Auctions_Seller_Status] ON [Auctions] ([SellerId], [Status]);
GO


CREATE INDEX [IX_Auctions_Status_CurrentPrice] ON [Auctions] ([Status], [CurrentPrice]);
GO


CREATE INDEX [IX_Auctions_Status_EndAt] ON [Auctions] ([Status], [EndAt]);
GO


CREATE INDEX [IX_Auctions_WinnerId] ON [Auctions] ([WinnerId]);
GO


CREATE INDEX [IX_AuctionWatches_UserId] ON [AuctionWatches] ([UserId]);
GO


CREATE UNIQUE INDEX [UX_AuctionWatches_Auction_User] ON [AuctionWatches] ([AuctionId], [UserId]);
GO


CREATE INDEX [IX_BankAccount_UserId_IsDeleted] ON [BankAccount] ([UserId], [IsDeleted]);
GO


CREATE UNIQUE INDEX [UX_BankAccount_DefaultPerUser] ON [BankAccount] ([UserId]) WHERE [IsDefault] = 1 AND [IsDeleted] = 0;
GO


CREATE INDEX [IX_Bids_Auction_PlacedAt] ON [Bids] ([AuctionId], [PlacedAt]);
GO


CREATE INDEX [IX_Bids_BidderId] ON [Bids] ([BidderId]);
GO


CREATE UNIQUE INDEX [UX_Bids_OneLeadingPerAuction] ON [Bids] ([AuctionId]) WHERE [Status] = 'Leading';
GO


CREATE INDEX [IX_ChatRoomMembers_UserId] ON [ChatRoomMembers] ([UserId]);
GO


CREATE UNIQUE INDEX [UX_ChatRoomMembers_Room_User] ON [ChatRoomMembers] ([RoomId], [UserId]);
GO


CREATE INDEX [IX_ChatRooms_AuctionId] ON [ChatRooms] ([AuctionId]);
GO


CREATE INDEX [IX_ChatRooms_CommissionId] ON [ChatRooms] ([CommissionId]);
GO


CREATE INDEX [IX_ChatRooms_LastMessageAt] ON [ChatRooms] ([LastMessageAt]);
GO


CREATE UNIQUE INDEX [IX_ClientProfiles_UserId] ON [ClientProfiles] ([UserId]);
GO


CREATE UNIQUE INDEX [IX_ClientProfiles_Username] ON [ClientProfiles] ([Username]) WHERE [Username] IS NOT NULL;
GO


CREATE INDEX [IX_ClientReviews_ClientId] ON [ClientReviews] ([ClientId]);
GO


CREATE INDEX [IX_ClientReviews_CommissionId] ON [ClientReviews] ([CommissionId]);
GO


CREATE INDEX [IX_ClientReviews_CreatorId] ON [ClientReviews] ([CreatorId]);
GO


CREATE INDEX [IX_CollectionArtworks_ArtworkId] ON [CollectionArtworks] ([ArtworkId]);
GO


CREATE INDEX [IX_Commissions_ClientId_CreatedAt] ON [Commissions] ([ClientId], [CreatedAt] DESC);
GO


CREATE INDEX [IX_Commissions_CreatorId_CreatedAt] ON [Commissions] ([CreatorId], [CreatedAt] DESC);
GO


CREATE INDEX [IX_Commissions_Status_UpdatedAt] ON [Commissions] ([Status], [UpdatedAt] DESC);
GO


CREATE INDEX [IX_CommissionServices_CreatorProfileId] ON [CommissionServices] ([CreatorProfileId]);
GO


CREATE INDEX [IX_CreatorApplications_ApplicantId_Status] ON [CreatorApplications] ([ApplicantId], [Status]);
GO


CREATE INDEX [IX_CreatorApplications_ReviewedByModId] ON [CreatorApplications] ([ReviewedByModId]);
GO


CREATE INDEX [IX_CreatorAssets_CreatorProfileId] ON [CreatorAssets] ([CreatorProfileId]);
GO


CREATE INDEX [IX_CreatorAutoReplySettings_CreatorProfileId] ON [CreatorAutoReplySettings] ([CreatorProfileId]);
GO


CREATE INDEX [IX_CreatorFaqs_CreatorProfileId] ON [CreatorFaqs] ([CreatorProfileId]);
GO


CREATE UNIQUE INDEX [IX_CreatorProfiles_UserId] ON [CreatorProfiles] ([UserId]);
GO


CREATE INDEX [IX_CreatorReviews_CreatorProfileId] ON [CreatorReviews] ([CreatorProfileId]);
GO


CREATE INDEX [IX_CreatorTerms_CreatorProfileId] ON [CreatorTerms] ([CreatorProfileId]);
GO


CREATE INDEX [IX_CreatorWorkItems_CreatorProfileId] ON [CreatorWorkItems] ([CreatorProfileId]);
GO


CREATE INDEX [IX_CriteriaScores_GradedByJuryId] ON [CriteriaScores] ([GradedByJuryId]);
GO


CREATE INDEX [IX_CriteriaScores_SubmissionId] ON [CriteriaScores] ([SubmissionId]);
GO


CREATE INDEX [IX_DeadlineReminders_CommissionId] ON [DeadlineReminders] ([CommissionId]);
GO


CREATE INDEX [IX_DeadlineReminders_MilestoneId] ON [DeadlineReminders] ([MilestoneId]);
GO


CREATE INDEX [IX_DeadlineReminders_Pending_RemindAt] ON [DeadlineReminders] ([SentAt], [RemindAt]);
GO


CREATE INDEX [IX_Deliverables_RecipientId] ON [Deliverables] ([RecipientId]);
GO


CREATE UNIQUE INDEX [UX_Deliverables_Auction] ON [Deliverables] ([AuctionId]);
GO


CREATE UNIQUE INDEX [IX_Disputes_CommissionId] ON [Disputes] ([CommissionId]);
GO


CREATE INDEX [IX_EmailVerificationCodes_Email] ON [EmailVerificationCodes] ([Email]);
GO


CREATE INDEX [IX_EscrowTransactions_PayeeId] ON [EscrowTransactions] ([PayeeId]);
GO


CREATE INDEX [IX_EscrowTransactions_PayerId] ON [EscrowTransactions] ([PayerId]);
GO


CREATE UNIQUE INDEX [UX_EscrowTransactions_Auction] ON [EscrowTransactions] ([AuctionId]);
GO


CREATE INDEX [IX_EventCriteria_EventId] ON [EventCriteria] ([EventId]);
GO


CREATE INDEX [IX_EventSubmissions_ArtworkId] ON [EventSubmissions] ([ArtworkId]);
GO


CREATE INDEX [IX_EventSubmissions_EventId_Score_VoteCount] ON [EventSubmissions] ([EventId], [Score], [VoteCount]);
GO


CREATE INDEX [IX_EventSubmissions_EventId_SubmitterId] ON [EventSubmissions] ([EventId], [SubmitterId]);
GO


CREATE INDEX [IX_EventSubmissions_SubmitterId] ON [EventSubmissions] ([SubmitterId]);
GO


CREATE INDEX [IX_EventVotes_VoterId] ON [EventVotes] ([VoterId]);
GO


CREATE INDEX [IX_Follows_CreatorProfileId] ON [Follows] ([CreatorProfileId]);
GO


CREATE UNIQUE INDEX [IX_Invitations_EventId_SentToCreatorId] ON [Invitations] ([EventId], [SentToCreatorId]) WHERE [Status] = 'Pending';
GO


CREATE INDEX [IX_Invitations_EventId_Status] ON [Invitations] ([EventId], [Status]);
GO


CREATE INDEX [IX_Invitations_SentFromAdminId] ON [Invitations] ([SentFromAdminId]);
GO


CREATE INDEX [IX_Invitations_SentToCreatorId_Status] ON [Invitations] ([SentToCreatorId], [Status]);
GO


CREATE INDEX [IX_Juries_CreatorId] ON [Juries] ([CreatorId]);
GO


CREATE UNIQUE INDEX [IX_Juries_EventId] ON [Juries] ([EventId]) WHERE [IsHeadJury] = 1;
GO


CREATE UNIQUE INDEX [IX_Juries_EventId_CreatorId] ON [Juries] ([EventId], [CreatorId]);
GO


CREATE INDEX [IX_MessageAttachments_MessageId] ON [MessageAttachments] ([MessageId]);
GO


CREATE INDEX [IX_MessageAttachments_UploadedBy] ON [MessageAttachments] ([UploadedBy]);
GO


CREATE INDEX [IX_Messages_Commission_SentAt] ON [Messages] ([CommissionId], [SentAt]);
GO


CREATE INDEX [IX_Messages_Room_SentAt] ON [Messages] ([RoomId], [SentAt]);
GO


CREATE INDEX [IX_Messages_SenderId] ON [Messages] ([SenderId]);
GO


CREATE INDEX [IX_Milestones_CommissionId] ON [Milestones] ([CommissionId]);
GO


CREATE INDEX [IX_Notification_UserId_CreatedAt] ON [Notification] ([UserId], [CreatedAt]);
GO


CREATE INDEX [IX_Notification_UserId_IsRead] ON [Notification] ([UserId], [IsRead]);
GO


CREATE UNIQUE INDEX [UX_Notification_UserId_DedupKey] ON [Notification] ([UserId], [DedupKey]) WHERE [DedupKey] IS NOT NULL;
GO


CREATE INDEX [IX_PaymentOrder_Status] ON [PaymentOrder] ([Status]);
GO


CREATE INDEX [IX_PaymentOrder_UserId_CreatedAt] ON [PaymentOrder] ([UserId], [CreatedAt] DESC);
GO


CREATE INDEX [IX_PaymentOrder_WalletId] ON [PaymentOrder] ([WalletId]);
GO


CREATE UNIQUE INDEX [UX_PaymentOrder_Gateway_PayOsOrderCode] ON [PaymentOrder] ([Gateway], [PayOsOrderCode]);
GO


CREATE UNIQUE INDEX [UX_PaymentOrder_Gateway_TransactionRef] ON [PaymentOrder] ([Gateway], [TransactionRef]) WHERE [TransactionRef] IS NOT NULL;
GO


CREATE UNIQUE INDEX [UX_PaymentOrder_OrderRef] ON [PaymentOrder] ([OrderRef]);
GO


CREATE INDEX [IX_PayoutRequest_BankAccountId] ON [PayoutRequest] ([BankAccountId]);
GO


CREATE INDEX [IX_PayoutRequest_Status] ON [PayoutRequest] ([Status]);
GO


CREATE INDEX [IX_PayoutRequest_UserId_CreatedAt] ON [PayoutRequest] ([UserId], [CreatedAt] DESC);
GO


CREATE INDEX [IX_PayoutRequest_WalletId] ON [PayoutRequest] ([WalletId]);
GO


CREATE UNIQUE INDEX [UX_PlatformConfig_Key] ON [PlatformConfig] ([Key]);
GO


CREATE INDEX [IX_PlatformEvents_CreatedAt] ON [PlatformEvents] ([CreatedAt]);
GO


CREATE INDEX [IX_PlatformEvents_CreatedByAdminId] ON [PlatformEvents] ([CreatedByAdminId]);
GO


CREATE INDEX [IX_PlatformEvents_Status_SubmissionStartAt_SubmissionEndAt] ON [PlatformEvents] ([Status], [SubmissionStartAt], [SubmissionEndAt]);
GO


CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
GO


CREATE UNIQUE INDEX [UX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
GO


CREATE INDEX [IX_RevenueSnapshots_Scope_Date] ON [RevenueSnapshots] ([Scope], [SnapshotDate]);
GO


CREATE UNIQUE INDEX [UX_RevenueSnapshots_Creator_Scope_Date] ON [RevenueSnapshots] ([CreatorId], [Scope], [SnapshotDate]);
GO


CREATE UNIQUE INDEX [IX_Reviews_CommissionId] ON [Reviews] ([CommissionId]);
GO


CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_SubmissionDisputes_EventId] ON [SubmissionDisputes] ([EventId]);
GO


CREATE INDEX [IX_SubmissionDisputes_SubmissionId] ON [SubmissionDisputes] ([SubmissionId]);
GO


CREATE UNIQUE INDEX [IX_Tags_Name] ON [Tags] ([Name]);
GO


CREATE UNIQUE INDEX [UX_TwoFactorRecoveryCodes_UserId_CodeHash] ON [TwoFactorRecoveryCodes] ([UserId], [CodeHash]);
GO


CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
GO


CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);
GO


CREATE UNIQUE INDEX [UX_UserReminderSettings_UserId] ON [UserReminderSettings] ([UserId]);
GO


CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
GO


CREATE UNIQUE INDEX [IX_Users_NormalizedLoginUsername] ON [Users] ([NormalizedLoginUsername]) WHERE [NormalizedLoginUsername] IS NOT NULL;
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE INDEX [IX_UserSanctions_ActionByAdminId] ON [UserSanctions] ([ActionByAdminId]);
GO


CREATE INDEX [IX_UserSanctions_UserId_CreatedAt] ON [UserSanctions] ([UserId], [CreatedAt]);
GO


CREATE INDEX [IX_Voucher_Active_Window] ON [Voucher] ([IsActive], [StartDate], [EndDate]);
GO


CREATE INDEX [IX_Voucher_CreatedByUserId] ON [Voucher] ([CreatedByUserId]);
GO


CREATE INDEX [IX_Voucher_IsDeleted] ON [Voucher] ([IsDeleted]);
GO


CREATE UNIQUE INDEX [UX_Voucher_VoucherCode] ON [Voucher] ([VoucherCode]);
GO


CREATE INDEX [IX_VoucherRedemption_UserId] ON [VoucherRedemption] ([UserId]);
GO


CREATE INDEX [IX_VoucherRedemption_Voucher_User] ON [VoucherRedemption] ([VoucherId], [UserId]);
GO


CREATE UNIQUE INDEX [UX_VoucherRedemption_Ref] ON [VoucherRedemption] ([RefType], [RefId]);
GO


CREATE UNIQUE INDEX [UX_Wallet_UserId] ON [Wallet] ([UserId]);
GO


CREATE INDEX [IX_WalletTransaction_WalletId_CreatedAt] ON [WalletTransaction] ([WalletId], [CreatedAt] DESC);
GO


CREATE UNIQUE INDEX [UX_WalletTransaction_Ref] ON [WalletTransaction] ([RefType], [RefId], [Type]) WHERE [RefId] IS NOT NULL;
GO


