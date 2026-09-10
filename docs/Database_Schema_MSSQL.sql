-- ============================================================
-- ART COMMISSION MARKETPLACE - DATABASE SCHEMA (Microsoft SQL Server)
-- Runnable script for SSMS / Azure Data Studio
-- Modules: Auth, Portfolio & Search, Commission Flow,
--          Payment/Escrow, Admin Panel, Real-time Chat/Notification
-- ============================================================

IF DB_ID(N'ArtCommissionDB') IS NULL
BEGIN
    CREATE DATABASE ArtCommissionDB;
END
GO

USE ArtCommissionDB;
GO

-- ============================================================
-- 1. AUTH & ACCOUNT (module: Auth)  -> UC-001..004
-- ============================================================
CREATE TABLE dbo.Roles (
    RoleId          INT             IDENTITY(1,1) CONSTRAINT PK_Roles PRIMARY KEY,
    RoleName        NVARCHAR(20)    NOT NULL CONSTRAINT UQ_Roles_RoleName UNIQUE  -- Buyer, Artist, Admin
);
GO

CREATE TABLE dbo.Users (
    UserId          INT             IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    RoleId          INT             NOT NULL,
    Email           NVARCHAR(256)   NOT NULL,
    PasswordHash    NVARCHAR(256)   NOT NULL,
    FullName        NVARCHAR(100)   NOT NULL,
    AvatarUrl       NVARCHAR(500)   NULL,
    PhoneNumber     NVARCHAR(20)    NULL,
    Status          NVARCHAR(20)    NOT NULL CONSTRAINT DF_Users_Status DEFAULT ('Active'), -- Active, Banned, PendingVerification
    EmailVerifiedAt DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2       NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId),
    CONSTRAINT CK_Users_Status CHECK (Status IN ('Active','Banned','PendingVerification'))
);
GO

CREATE TABLE dbo.PasswordResetTokens (          -- UC-003
    TokenId         INT             IDENTITY(1,1) CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
    UserId          INT             NOT NULL CONSTRAINT FK_PasswordResetTokens_Users REFERENCES dbo.Users(UserId),
    Token           NVARCHAR(200)   NOT NULL,
    ExpiresAt       DATETIME2       NOT NULL,
    UsedAt          DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.RefreshTokens (                -- Auth API: refresh token
    RefreshTokenId  INT             IDENTITY(1,1) CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId          INT             NOT NULL CONSTRAINT FK_RefreshTokens_Users REFERENCES dbo.Users(UserId),
    TokenHash       NVARCHAR(256)   NOT NULL,
    ExpiresAt       DATETIME2       NOT NULL,
    RevokedAt       DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ============================================================
-- 2. PORTFOLIO & SEARCH (module: Portfolio & Search) -> UC-005..014
-- ============================================================
CREATE TABLE dbo.ArtistProfiles (               -- UC-014, màn hình Artist Overview
    ArtistProfileId     INT             IDENTITY(1,1) CONSTRAINT PK_ArtistProfiles PRIMARY KEY,
    UserId              INT             NOT NULL CONSTRAINT UQ_ArtistProfiles_UserId UNIQUE
                                             CONSTRAINT FK_ArtistProfiles_Users REFERENCES dbo.Users(UserId),
    Headline            NVARCHAR(200)   NULL,
    Bio                 NVARCHAR(MAX)   NULL,
    Specialties         NVARCHAR(500)   NULL,   -- csv/tags: anime, portrait, chibi...
    IsAcceptingOrders   BIT             NOT NULL CONSTRAINT DF_ArtistProfiles_Accepting DEFAULT (1),
    IsApproved          BIT             NOT NULL CONSTRAINT DF_ArtistProfiles_Approved DEFAULT (0),   -- UC-025
    ApprovedByUserId    INT             NULL CONSTRAINT FK_ArtistProfiles_ApprovedBy REFERENCES dbo.Users(UserId),
    ApprovedAt          DATETIME2       NULL,
    RatingAverage        DECIMAL(3,2)   NOT NULL CONSTRAINT DF_ArtistProfiles_RatingAvg DEFAULT (0),
    RatingCount          INT            NOT NULL CONSTRAINT DF_ArtistProfiles_RatingCount DEFAULT (0),
    FollowerCount        INT            NOT NULL CONSTRAINT DF_ArtistProfiles_FollowerCount DEFAULT (0),
    CreatedAt            DATETIME2      NOT NULL CONSTRAINT DF_ArtistProfiles_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt             DATETIME2     NULL
);
GO

CREATE TABLE dbo.Follows (                      -- UC-010
    FollowId        INT             IDENTITY(1,1) CONSTRAINT PK_Follows PRIMARY KEY,
    FollowerUserId  INT             NOT NULL CONSTRAINT FK_Follows_Users REFERENCES dbo.Users(UserId),
    ArtistProfileId INT             NOT NULL CONSTRAINT FK_Follows_ArtistProfiles REFERENCES dbo.ArtistProfiles(ArtistProfileId),
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Follows_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Follows UNIQUE (FollowerUserId, ArtistProfileId)
);
GO

CREATE TABLE dbo.ServicePackages (              -- UC-013 Rate Card
    ServicePackageId    INT             IDENTITY(1,1) CONSTRAINT PK_ServicePackages PRIMARY KEY,
    ArtistProfileId     INT             NOT NULL CONSTRAINT FK_ServicePackages_ArtistProfiles REFERENCES dbo.ArtistProfiles(ArtistProfileId),
    Name                NVARCHAR(150)   NOT NULL,     -- e.g. "Half Body Colored"
    Description         NVARCHAR(MAX)   NULL,
    BasePrice           DECIMAL(12,2)   NOT NULL,
    DeliveryDays        INT             NOT NULL,
    RevisionLimit       INT             NOT NULL CONSTRAINT DF_ServicePackages_RevisionLimit DEFAULT (2),
    IsActive            BIT             NOT NULL CONSTRAINT DF_ServicePackages_IsActive DEFAULT (1),
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_ServicePackages_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_ServicePackages_BasePrice CHECK (BasePrice >= 0)
);
GO

CREATE TABLE dbo.Slots (                        -- UC-013 Slot management
    SlotId              INT             IDENTITY(1,1) CONSTRAINT PK_Slots PRIMARY KEY,
    ArtistProfileId     INT             NOT NULL CONSTRAINT UQ_Slots_ArtistProfileId UNIQUE
                                             CONSTRAINT FK_Slots_ArtistProfiles REFERENCES dbo.ArtistProfiles(ArtistProfileId),
    TotalSlots          INT             NOT NULL CONSTRAINT DF_Slots_TotalSlots DEFAULT (0),
    UsedSlots           INT             NOT NULL CONSTRAINT DF_Slots_UsedSlots DEFAULT (0),
    IsOpen              BIT             NOT NULL CONSTRAINT DF_Slots_IsOpen DEFAULT (1),
    UpdatedAt           DATETIME2       NULL,
    CONSTRAINT CK_Slots_UsedNotOverTotal CHECK (UsedSlots <= TotalSlots)
);
GO

CREATE TABLE dbo.Artworks (                     -- UC-011 Upload, UC-008 View detail
    ArtworkId           INT             IDENTITY(1,1) CONSTRAINT PK_Artworks PRIMARY KEY,
    ArtistProfileId     INT             NOT NULL CONSTRAINT FK_Artworks_ArtistProfiles REFERENCES dbo.ArtistProfiles(ArtistProfileId),
    Title               NVARCHAR(200)   NOT NULL,
    Description         NVARCHAR(MAX)   NULL,
    ImageUrl            NVARCHAR(500)   NOT NULL,     -- Cloudinary URL
    ThumbnailUrl        NVARCHAR(500)   NULL,
    IsAIGeneratedFlag   BIT             NOT NULL CONSTRAINT DF_Artworks_AIFlag DEFAULT (0),  -- AI Detection API result
    AIDetectionScore    DECIMAL(5,4)    NULL,
    ModerationStatus    NVARCHAR(20)    NOT NULL CONSTRAINT DF_Artworks_ModerationStatus DEFAULT ('Pending'), -- UC-026
    ViewCount            INT            NOT NULL CONSTRAINT DF_Artworks_ViewCount DEFAULT (0),
    CreatedAt            DATETIME2      NOT NULL CONSTRAINT DF_Artworks_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt             DATETIME2     NULL,
    CONSTRAINT CK_Artworks_ModerationStatus CHECK (ModerationStatus IN ('Pending','Approved','Rejected'))
);
GO

CREATE TABLE dbo.Tags (
    TagId           INT             IDENTITY(1,1) CONSTRAINT PK_Tags PRIMARY KEY,
    TagName         NVARCHAR(50)    NOT NULL CONSTRAINT UQ_Tags_TagName UNIQUE,
    Source          NVARCHAR(20)    NOT NULL CONSTRAINT DF_Tags_Source DEFAULT ('Manual')  -- Manual, AI
);
GO

CREATE TABLE dbo.ArtworkTags (
    ArtworkId       INT             NOT NULL CONSTRAINT FK_ArtworkTags_Artworks REFERENCES dbo.Artworks(ArtworkId),
    TagId           INT             NOT NULL CONSTRAINT FK_ArtworkTags_Tags REFERENCES dbo.Tags(TagId),
    CONSTRAINT PK_ArtworkTags PRIMARY KEY (ArtworkId, TagId)
);
GO

CREATE TABLE dbo.ArtworkModerationLogs (        -- UC-026 kiểm duyệt tranh mẫu
    ModerationLogId     INT             IDENTITY(1,1) CONSTRAINT PK_ArtworkModerationLogs PRIMARY KEY,
    ArtworkId           INT             NOT NULL CONSTRAINT FK_ArtworkModerationLogs_Artworks REFERENCES dbo.Artworks(ArtworkId),
    ReviewedByUserId    INT             NULL CONSTRAINT FK_ArtworkModerationLogs_Users REFERENCES dbo.Users(UserId),   -- NULL = AI scan
    Decision             NVARCHAR(20)   NOT NULL,   -- Approved, Rejected, Flagged
    Reason               NVARCHAR(500)  NULL,
    ReviewedAt            DATETIME2     NOT NULL CONSTRAINT DF_ArtworkModerationLogs_ReviewedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ============================================================
-- 3. COMMISSION FLOW (module: Commission Flow) -> UC-015..020
-- ============================================================
CREATE TABLE dbo.Commissions (                  -- UC-015 Create Request
    CommissionId        INT             IDENTITY(1,1) CONSTRAINT PK_Commissions PRIMARY KEY,
    BuyerUserId          INT            NOT NULL CONSTRAINT FK_Commissions_Buyer REFERENCES dbo.Users(UserId),
    ArtistProfileId      INT            NOT NULL CONSTRAINT FK_Commissions_ArtistProfiles REFERENCES dbo.ArtistProfiles(ArtistProfileId),
    ServicePackageId     INT            NULL CONSTRAINT FK_Commissions_ServicePackages REFERENCES dbo.ServicePackages(ServicePackageId),
    Title                NVARCHAR(200)  NOT NULL,
    Description          NVARCHAR(MAX)  NULL,
    ReferenceImageUrls   NVARCHAR(MAX)  NULL,   -- JSON array
    AgreedPrice          DECIMAL(12,2)  NOT NULL,
    Deadline             DATETIME2      NULL,
    Status               NVARCHAR(30)   NOT NULL CONSTRAINT DF_Commissions_Status DEFAULT ('Requested'),
        -- Requested -> Accepted/Rejected -> InProgress -> UnderReview -> Completed / Disputed / Cancelled
    CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_Commissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt              DATETIME2    NULL,
    CONSTRAINT CK_Commissions_Status CHECK (Status IN
        ('Requested','Accepted','Rejected','InProgress','UnderReview','Completed','Disputed','Cancelled')),
    CONSTRAINT CK_Commissions_AgreedPrice CHECK (AgreedPrice >= 0)
);
GO

CREATE TABLE dbo.CommissionStatusHistory (      -- state machine audit
    HistoryId           INT             IDENTITY(1,1) CONSTRAINT PK_CommissionStatusHistory PRIMARY KEY,
    CommissionId         INT            NOT NULL CONSTRAINT FK_CommissionStatusHistory_Commissions REFERENCES dbo.Commissions(CommissionId),
    FromStatus           NVARCHAR(30)   NULL,
    ToStatus              NVARCHAR(30)  NOT NULL,
    ChangedByUserId       INT           NOT NULL CONSTRAINT FK_CommissionStatusHistory_Users REFERENCES dbo.Users(UserId),
    Note                  NVARCHAR(500) NULL,
    ChangedAt              DATETIME2    NOT NULL CONSTRAINT DF_CommissionStatusHistory_ChangedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.CommissionProgress (           -- UC-018 Sketch/Lineart/Coloring/Final
    ProgressId           INT             IDENTITY(1,1) CONSTRAINT PK_CommissionProgress PRIMARY KEY,
    CommissionId          INT            NOT NULL CONSTRAINT FK_CommissionProgress_Commissions REFERENCES dbo.Commissions(CommissionId),
    Stage                 NVARCHAR(20)   NOT NULL,  -- Sketch, Lineart, Coloring, Final
    ImageUrl               NVARCHAR(500) NOT NULL,
    Status                  NVARCHAR(20) NOT NULL CONSTRAINT DF_CommissionProgress_Status DEFAULT ('PendingReview'),
    UploadedAt               DATETIME2   NOT NULL CONSTRAINT DF_CommissionProgress_UploadedAt DEFAULT (SYSUTCDATETIME()),
    ReviewedAt                DATETIME2  NULL,
    CONSTRAINT CK_CommissionProgress_Stage CHECK (Stage IN ('Sketch','Lineart','Coloring','Final')),
    CONSTRAINT CK_CommissionProgress_Status CHECK (Status IN ('PendingReview','Approved','RevisionRequested'))
);
GO

CREATE TABLE dbo.RevisionRequests (             -- UC-019 Yêu cầu sửa
    RevisionId            INT             IDENTITY(1,1) CONSTRAINT PK_RevisionRequests PRIMARY KEY,
    ProgressId             INT            NOT NULL CONSTRAINT FK_RevisionRequests_CommissionProgress REFERENCES dbo.CommissionProgress(ProgressId),
    RequestedByUserId       INT           NOT NULL CONSTRAINT FK_RevisionRequests_Users REFERENCES dbo.Users(UserId),
    Note                     NVARCHAR(MAX) NOT NULL,
    CreatedAt                 DATETIME2   NOT NULL CONSTRAINT DF_RevisionRequests_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.CommissionReviews (            -- rating sau khi Complete (UC-020)
    ReviewId              INT             IDENTITY(1,1) CONSTRAINT PK_CommissionReviews PRIMARY KEY,
    CommissionId            INT           NOT NULL CONSTRAINT UQ_CommissionReviews_CommissionId UNIQUE
                                               CONSTRAINT FK_CommissionReviews_Commissions REFERENCES dbo.Commissions(CommissionId),
    RatingByUserId           INT          NOT NULL CONSTRAINT FK_CommissionReviews_Users REFERENCES dbo.Users(UserId),
    Rating                    TINYINT     NOT NULL,
    Comment                    NVARCHAR(MAX) NULL,
    CreatedAt                   DATETIME2  NOT NULL CONSTRAINT DF_CommissionReviews_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_CommissionReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
);
GO

-- ============================================================
-- 4. PAYMENT / ESCROW & WALLET (module: Payment/Escrow) -> UC-021..024
-- ============================================================
CREATE TABLE dbo.Wallets (                      -- UC-023
    WalletId             INT             IDENTITY(1,1) CONSTRAINT PK_Wallets PRIMARY KEY,
    UserId                INT            NOT NULL CONSTRAINT UQ_Wallets_UserId UNIQUE
                                              CONSTRAINT FK_Wallets_Users REFERENCES dbo.Users(UserId),
    Balance               DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Wallets_Balance DEFAULT (0),
    UpdatedAt              DATETIME2     NULL,
    CONSTRAINT CK_Wallets_Balance CHECK (Balance >= 0)
);
GO

CREATE TABLE dbo.EscrowHolds (                  -- UC-021 Deposit / escrow logic
    EscrowId              INT             IDENTITY(1,1) CONSTRAINT PK_EscrowHolds PRIMARY KEY,
    CommissionId           INT            NOT NULL CONSTRAINT UQ_EscrowHolds_CommissionId UNIQUE
                                               CONSTRAINT FK_EscrowHolds_Commissions REFERENCES dbo.Commissions(CommissionId),
    AmountHeld             DECIMAL(14,2)  NOT NULL,
    AmountReleased          DECIMAL(14,2) NOT NULL CONSTRAINT DF_EscrowHolds_AmountReleased DEFAULT (0),
    Status                   NVARCHAR(20) NOT NULL CONSTRAINT DF_EscrowHolds_Status DEFAULT ('Held'),
    CreatedAt                 DATETIME2   NOT NULL CONSTRAINT DF_EscrowHolds_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastReleasedAt              DATETIME2 NULL,
    CONSTRAINT CK_EscrowHolds_Status CHECK (Status IN ('Held','PartiallyReleased','FullyReleased','Refunded')),
    CONSTRAINT CK_EscrowHolds_Amounts CHECK (AmountReleased <= AmountHeld)
);
GO

CREATE TABLE dbo.Transactions (                 -- UC-022 lịch sử giao dịch
    TransactionId          INT             IDENTITY(1,1) CONSTRAINT PK_Transactions PRIMARY KEY,
    UserId                  INT            NOT NULL CONSTRAINT FK_Transactions_Users REFERENCES dbo.Users(UserId),
    CommissionId             INT           NULL CONSTRAINT FK_Transactions_Commissions REFERENCES dbo.Commissions(CommissionId),
    Type                      NVARCHAR(20) NOT NULL,  -- DepositIn, EscrowRelease, Payout, Refund, FeeDeduction
    Amount                     DECIMAL(14,2) NOT NULL,
    Status                      NVARCHAR(20) NOT NULL CONSTRAINT DF_Transactions_Status DEFAULT ('Pending'),
    PaymentMethod                NVARCHAR(20) NULL,   -- Mock, VNPAY, MoMo
    GatewayReference               NVARCHAR(200) NULL,
    CreatedAt                       DATETIME2 NOT NULL CONSTRAINT DF_Transactions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Transactions_Status CHECK (Status IN ('Pending','Success','Failed')),
    CONSTRAINT CK_Transactions_Type CHECK (Type IN ('DepositIn','EscrowRelease','Payout','Refund','FeeDeduction'))
);
GO

CREATE TABLE dbo.PayoutRequests (               -- UC-024
    PayoutId               INT             IDENTITY(1,1) CONSTRAINT PK_PayoutRequests PRIMARY KEY,
    WalletId                 INT           NOT NULL CONSTRAINT FK_PayoutRequests_Wallets REFERENCES dbo.Wallets(WalletId),
    Amount                    DECIMAL(14,2) NOT NULL,
    Status                     NVARCHAR(20) NOT NULL CONSTRAINT DF_PayoutRequests_Status DEFAULT ('Pending'),
    RequestedAt                 DATETIME2   NOT NULL CONSTRAINT DF_PayoutRequests_RequestedAt DEFAULT (SYSUTCDATETIME()),
    ProcessedByUserId             INT       NULL CONSTRAINT FK_PayoutRequests_Users REFERENCES dbo.Users(UserId),
    ProcessedAt                    DATETIME2 NULL,
    CONSTRAINT CK_PayoutRequests_Status CHECK (Status IN ('Pending','Approved','Rejected','Paid')),
    CONSTRAINT CK_PayoutRequests_Amount CHECK (Amount > 0)
);
GO

CREATE TABLE dbo.PlatformFeeConfig (            -- UC-029 Quản lý phí sàn
    FeeConfigId            INT             IDENTITY(1,1) CONSTRAINT PK_PlatformFeeConfig PRIMARY KEY,
    FeePercent               DECIMAL(5,2)  NOT NULL,   -- e.g. 10.00
    EffectiveFrom              DATETIME2   NOT NULL,
    UpdatedByUserId              INT       NOT NULL CONSTRAINT FK_PlatformFeeConfig_Users REFERENCES dbo.Users(UserId),
    CreatedAt                     DATETIME2 NOT NULL CONSTRAINT DF_PlatformFeeConfig_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_PlatformFeeConfig_FeePercent CHECK (FeePercent BETWEEN 0 AND 100)
);
GO

-- ============================================================
-- 5. ADMIN PANEL (module: Admin Panel) -> UC-025..029
-- ============================================================
CREATE TABLE dbo.Disputes (                     -- UC-028
    DisputeId              INT             IDENTITY(1,1) CONSTRAINT PK_Disputes PRIMARY KEY,
    CommissionId             INT           NOT NULL CONSTRAINT FK_Disputes_Commissions REFERENCES dbo.Commissions(CommissionId),
    RaisedByUserId            INT          NOT NULL CONSTRAINT FK_Disputes_RaisedBy REFERENCES dbo.Users(UserId),
    Reason                     NVARCHAR(MAX) NOT NULL,
    Status                      NVARCHAR(20) NOT NULL CONSTRAINT DF_Disputes_Status DEFAULT ('Open'),
    ResolvedByUserId               INT     NULL CONSTRAINT FK_Disputes_ResolvedBy REFERENCES dbo.Users(UserId),
    ResolutionNote                  NVARCHAR(MAX) NULL,
    CreatedAt                        DATETIME2 NOT NULL CONSTRAINT DF_Disputes_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ResolvedAt                        DATETIME2 NULL,
    CONSTRAINT CK_Disputes_Status CHECK (Status IN ('Open','UnderReview','Resolved','Rejected'))
);
GO

CREATE TABLE dbo.UserSanctions (                -- UC-027 ban/unban
    SanctionId              INT             IDENTITY(1,1) CONSTRAINT PK_UserSanctions PRIMARY KEY,
    UserId                    INT           NOT NULL CONSTRAINT FK_UserSanctions_Users REFERENCES dbo.Users(UserId),
    ActionType                 NVARCHAR(20) NOT NULL,  -- Ban, Unban, Warn
    Reason                       NVARCHAR(500) NULL,
    ActionByUserId                 INT       NOT NULL CONSTRAINT FK_UserSanctions_ActionBy REFERENCES dbo.Users(UserId),
    CreatedAt                        DATETIME2 NOT NULL CONSTRAINT DF_UserSanctions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_UserSanctions_ActionType CHECK (ActionType IN ('Ban','Unban','Warn'))
);
GO

CREATE TABLE dbo.AuditLogs (                    -- 5.4 audit trail
    AuditLogId              INT             IDENTITY(1,1) CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId                    INT           NULL CONSTRAINT FK_AuditLogs_Users REFERENCES dbo.Users(UserId),
    Action                      NVARCHAR(100) NOT NULL,
    EntityType                    NVARCHAR(50) NOT NULL,
    EntityId                        INT       NULL,
    Detail                            NVARCHAR(MAX) NULL,
    CreatedAt                          DATETIME2 NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ============================================================
-- 6. REAL-TIME CHAT & NOTIFICATION (module: SignalR) -> UC-030..031
-- ============================================================
CREATE TABLE dbo.Workrooms (                    -- 1-1 with Commission, màn hình #12
    WorkroomId              INT             IDENTITY(1,1) CONSTRAINT PK_Workrooms PRIMARY KEY,
    CommissionId              INT           NOT NULL CONSTRAINT UQ_Workrooms_CommissionId UNIQUE
                                                 CONSTRAINT FK_Workrooms_Commissions REFERENCES dbo.Commissions(CommissionId),
    CreatedAt                   DATETIME2   NOT NULL CONSTRAINT DF_Workrooms_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.ChatMessages (                 -- UC-030
    MessageId                INT             IDENTITY(1,1) CONSTRAINT PK_ChatMessages PRIMARY KEY,
    WorkroomId                 INT           NOT NULL CONSTRAINT FK_ChatMessages_Workrooms REFERENCES dbo.Workrooms(WorkroomId),
    SenderUserId                 INT         NOT NULL CONSTRAINT FK_ChatMessages_Users REFERENCES dbo.Users(UserId),
    Content                        NVARCHAR(MAX) NULL,
    AttachmentUrl                    NVARCHAR(500) NULL,
    SentAt                             DATETIME2 NOT NULL CONSTRAINT DF_ChatMessages_SentAt DEFAULT (SYSUTCDATETIME()),
    IsRead                               BIT     NOT NULL CONSTRAINT DF_ChatMessages_IsRead DEFAULT (0)
);
GO

CREATE TABLE dbo.Notifications (                -- UC-031
    NotificationId            INT             IDENTITY(1,1) CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId                      INT           NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES dbo.Users(UserId),
    Type                          NVARCHAR(40) NOT NULL,  -- CommissionAccepted, ProgressUploaded, PayoutApproved, ...
    Title                          NVARCHAR(200) NOT NULL,
    Content                          NVARCHAR(MAX) NULL,
    RelatedEntityType                  NVARCHAR(50) NULL,  -- Commission, Dispute, Payout...
    RelatedEntityId                      INT     NULL,
    IsRead                                  BIT   NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    CreatedAt                                 DATETIME2 NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ============================================================
-- INDEXES (search / performance-critical paths)
-- ============================================================
CREATE INDEX IX_Artworks_ArtistProfileId ON dbo.Artworks(ArtistProfileId);
CREATE INDEX IX_Artworks_ModerationStatus ON dbo.Artworks(ModerationStatus);
CREATE INDEX IX_Commissions_BuyerUserId ON dbo.Commissions(BuyerUserId);
CREATE INDEX IX_Commissions_ArtistProfileId ON dbo.Commissions(ArtistProfileId);
CREATE INDEX IX_Commissions_Status ON dbo.Commissions(Status);
CREATE INDEX IX_Transactions_UserId ON dbo.Transactions(UserId);
CREATE INDEX IX_Notifications_UserId_IsRead ON dbo.Notifications(UserId, IsRead);
CREATE INDEX IX_ChatMessages_WorkroomId ON dbo.ChatMessages(WorkroomId);
CREATE INDEX IX_Follows_ArtistProfileId ON dbo.Follows(ArtistProfileId);
GO

-- ============================================================
-- SEED: default roles
-- ============================================================
INSERT INTO dbo.Roles (RoleName) VALUES (N'Buyer'), (N'Artist'), (N'Admin');
GO
