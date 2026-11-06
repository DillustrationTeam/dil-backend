-- ============================================================================
-- DATABASE SCHEMA CREATION SCRIPT — ArtCommission (Dillustration)
-- Target DBMS: Microsoft SQL Server 2019+ / Azure SQL Database
-- Total Tables: 40 Tables (24 Core Entities + 16 Feature Entities)
-- Compatible with: EF Core 8 Code-First & Direct T-SQL Execution
-- NOTE: Auth module (AspNetUsers, RefreshTokens) is UNCHANGED as requested.
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'ArtCommissionDb')
BEGIN
    CREATE DATABASE ArtCommissionDb;
END
GO

USE ArtCommissionDb;
GO

-- ============================================================================
-- 1. IDENTITY & AUTH MODULE 
-- ============================================================================

-- Bảng Người dùng (Extends IdentityUser<Guid>)
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUsers (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY DEFAULT NEWID(),
        Email NVARCHAR(256) NOT NULL,
        NormalizedEmail NVARCHAR(256) NOT NULL,
        UserName NVARCHAR(256) NOT NULL,
        NormalizedUserName NVARCHAR(256) NOT NULL,
        PasswordHash NVARCHAR(MAX) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        IsVerified BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_AspNetUsers_NormalizedEmail ON dbo.AspNetUsers(NormalizedEmail);
    CREATE UNIQUE INDEX UX_AspNetUsers_NormalizedUserName ON dbo.AspNetUsers(NormalizedUserName);
END
GO

-- Bảng Refresh Tokens (Xác thực JWT Rotation)
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(450) NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        RevokedAt DATETIMEOFFSET NULL,
        CreatedByIp NVARCHAR(50) NULL,
        ReplacedByTokenHash NVARCHAR(450) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_RefreshTokens_AspNetUsers FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON dbo.RefreshTokens(TokenHash);
END
GO

-- ============================================================================
-- 2. ARTIST STUDIO & PORTFOLIO MODULE 
-- ============================================================================

-- Bảng Hồ sơ Creator / Artist
IF OBJECT_ID(N'dbo.CreatorProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CreatorProfiles (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CreatorProfiles PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(100) NOT NULL,
        Bio NVARCHAR(1000) NULL,
        RateCard NVARCHAR(MAX) NULL, -- JSON config bảng giá & dịch vụ
        CommissionSlots INT NOT NULL DEFAULT 0,
        CompletedOrdersCount INT NOT NULL DEFAULT 0, -- ⚡ [CẢI TIẾN] Thống kê số đơn thành công
        RatingAvg DECIMAL(3, 2) NOT NULL DEFAULT 0.00,
        IsAiVerified BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CreatorProfiles_AspNetUsers FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT UX_CreatorProfiles_UserId UNIQUE (UserId)
    );
END
GO

-- Bảng Tác phẩm Portfolio (Artwork)
IF OBJECT_ID(N'dbo.Artworks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Artworks (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Artworks PRIMARY KEY DEFAULT NEWID(),
        CreatorId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        ImageUrl NVARCHAR(500) NOT NULL,
        WatermarkedUrl NVARCHAR(500) NULL, -- ⚡ [CẢI TIẾN] Ảnh có watermark bảo hộ tác quyền
        Style NVARCHAR(100) NULL,
        ViewCount INT NOT NULL DEFAULT 0,   -- ⚡ [CẢI TIẾN] Thống kê lượt xem
        LikeCount INT NOT NULL DEFAULT 0,   -- ⚡ [CẢI TIẾN] Thống kê lượt thích
        Status NVARCHAR(50) NOT NULL DEFAULT 'Published', -- Draft, Published, Hidden, Flagged
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Artworks_CreatorProfiles FOREIGN KEY (CreatorId) REFERENCES dbo.CreatorProfiles(Id) ON DELETE CASCADE
    );
END
GO

-- Bảng Tag
IF OBJECT_ID(N'dbo.Tags', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tags (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tags PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(100) NOT NULL,
        IsAiGenerated BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT UX_Tags_Name UNIQUE (Name)
    );
END
GO

-- Bảng Liên kết Artwork & Tag (N-N)
IF OBJECT_ID(N'dbo.ArtworkTags', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtworkTags (
        ArtworkId UNIQUEIDENTIFIER NOT NULL,
        TagId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_ArtworkTags PRIMARY KEY (ArtworkId, TagId),
        CONSTRAINT FK_ArtworkTags_Artworks FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ArtworkTags_Tags FOREIGN KEY (TagId) REFERENCES dbo.Tags(Id) ON DELETE CASCADE
    );
END
GO

-- Bảng Follow Creator
IF OBJECT_ID(N'dbo.Follows', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Follows (
        FollowerId UNIQUEIDENTIFIER NOT NULL,
        FollowingId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Follows PRIMARY KEY (FollowerId, FollowingId),
        CONSTRAINT FK_Follows_Follower FOREIGN KEY (FollowerId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_Follows_Following FOREIGN KEY (FollowingId) REFERENCES dbo.CreatorProfiles(Id)
    );
END
GO

-- ============================================================================
-- 3. VOUCHER MODULE (⚡ Định nghĩa trước để tham chiếu FK trong Commission)
-- ============================================================================

-- Bảng Mã giảm giá (Voucher)
IF OBJECT_ID(N'dbo.Vouchers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vouchers (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Vouchers PRIMARY KEY DEFAULT NEWID(),
        Code NVARCHAR(50) NOT NULL,
        DiscountType NVARCHAR(50) NOT NULL DEFAULT 'Percentage', -- Percentage, FixedAmount
        DiscountValue DECIMAL(18, 2) NOT NULL,
        MinOrderAmount DECIMAL(18, 2) NULL,
        MaxUsage INT NULL,
        UsageCount INT NOT NULL DEFAULT 0,
        IssuedById UNIQUEIDENTIFIER NOT NULL,
        ValidFrom DATETIMEOFFSET NOT NULL,
        ValidUntil DATETIMEOFFSET NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Vouchers_IssuedBy FOREIGN KEY (IssuedById) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT UX_Vouchers_Code UNIQUE (Code)
    );
END
GO

-- ============================================================================
-- 4. COMMISSION & WORKROOM MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Commission (Đơn đặt vẽ)
IF OBJECT_ID(N'dbo.Commissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Commissions (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Commissions PRIMARY KEY DEFAULT NEWID(),
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        ClientId UNIQUEIDENTIFIER NOT NULL,
        CreatorId UNIQUEIDENTIFIER NOT NULL,
        VoucherId UNIQUEIDENTIFIER NULL,                  -- ⚡ [CẢI TIẾN] FK Mã giảm giá áp dụng
        DiscountAmount DECIMAL(18, 2) NOT NULL DEFAULT 0.00, -- ⚡ [CẢI TIẾN] Số tiền được giảm
        TotalPrice DECIMAL(18, 2) NOT NULL DEFAULT 0.00,     -- Giá gốc chưa giảm
        FinalPrice DECIMAL(18, 2) NOT NULL DEFAULT 0.00,     -- ⚡ [CẢI TIẾN] Giá thực tế sau giảm giá
        EscrowHeldAmount DECIMAL(18, 2) NOT NULL DEFAULT 0.00, -- ⚡ [CẢI TIẾN] Tiền đang bị khóa Escrow
        DisbursedAmount DECIMAL(18, 2) NOT NULL DEFAULT 0.00,  -- ⚡ [CẢI TIẾN] Tiền đã giải ngân cho Họa sĩ
        EscrowStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Deposited, PartialReleased, Released, Refunded, Disputed
        CurrentStage INT NOT NULL DEFAULT 1,
        Status NVARCHAR(50) NOT NULL DEFAULT 'PendingAcceptance', -- PendingAcceptance, InProgress, Completed, Cancelled, Disputed
        DeadlineAt DATETIMEOFFSET NULL,                        -- ⚡ [CẢI TIẾN] Hạn chót hoàn thành đơn
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Commissions_Client FOREIGN KEY (ClientId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_Commissions_Creator FOREIGN KEY (CreatorId) REFERENCES dbo.CreatorProfiles(Id),
        CONSTRAINT FK_Commissions_Voucher FOREIGN KEY (VoucherId) REFERENCES dbo.Vouchers(Id)
    );
END
GO

-- Bảng Milestone (Cột mốc tiến độ Workroom)
IF OBJECT_ID(N'dbo.Milestones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Milestones (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Milestones PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        Sequence INT NOT NULL DEFAULT 1,
        Title NVARCHAR(200) NOT NULL,
        Price DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, InProgress, Submitted, Approved, RevisionRequested
        WipPreviewUrl NVARCHAR(500) NULL,      -- Link ảnh xem thử có watermark
        WatermarkedUrl NVARCHAR(500) NULL,     -- ⚡ [CẢI TIẾN] Lưu file xem thử hạ chất lượng
        FinalDeliverableUrl NVARCHAR(500) NULL,-- ⚡ [CẢI TIẾN] File gốc HD bàn giao khi hoàn thành
        RevisionCount INT NOT NULL DEFAULT 0,  -- ⚡ [CẢI TIẾN] Đếm số lần yêu cầu sửa (Max theo Policy)
        SubmittedAt DATETIMEOFFSET NULL,       -- ⚡ [CẢI TIẾN] Thời điểm nộp WIP
        ApprovedAt DATETIMEOFFSET NULL,        -- ⚡ [CẢI TIẾN] Thời điểm Client duyệt
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Milestones_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id) ON DELETE CASCADE
    );
END
GO

-- Bảng Review (Đánh giá Đơn hoàn thành)
IF OBJECT_ID(N'dbo.Reviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reviews (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Reviews PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        ReviewerId UNIQUEIDENTIFIER NOT NULL,
        Rating INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
        Comment NVARCHAR(1000) NULL,
        ReviewerReply NVARCHAR(1000) NULL,
        IsVisible BIT NOT NULL DEFAULT 1,     -- ⚡ [CẢI TIẾN] Moderator có thể ẩn đánh giá vi phạm
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Reviews_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id),
        CONSTRAINT FK_Reviews_Reviewer FOREIGN KEY (ReviewerId) REFERENCES dbo.AspNetUsers(Id)
    );
END
GO

-- Bảng Tranh chấp Đơn hàng (CommissionDispute / Disputes)
IF OBJECT_ID(N'dbo.Disputes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Disputes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Disputes PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        RaisedById UNIQUEIDENTIFIER NOT NULL,
        Reason NVARCHAR(2000) NOT NULL,
        EvidenceUrls NVARCHAR(MAX) NULL,      -- ⚡ [CẢI TIẾN] JSON array ảnh bằng chứng tranh chấp
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, UnderReview, Resolved, Rejected
        Resolution NVARCHAR(50) NULL,          -- ⚡ [CẢI TIẾN] ClientWin100, ArtistWin100, SplitCustom
        ClientRefundAmount DECIMAL(18, 2) NULL,-- ⚡ [CẢI TIẾN] Số tiền hoàn cho Client theo phán quyết
        ArtistPayAmount DECIMAL(18, 2) NULL,   -- ⚡ [CẢI TIẾN] Số tiền trả cho Họa sĩ theo phán quyết
        AdminNote NVARCHAR(MAX) NULL,          -- Ghi chú phân xử của Admin / Moderator
        ResolvedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Disputes_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id),
        CONSTRAINT FK_Disputes_RaisedBy FOREIGN KEY (RaisedById) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT UX_Disputes_CommissionId UNIQUE (CommissionId)
    );
END
GO

-- ============================================================================
-- 5. PAYMENT & WALLET MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Thanh toán (Payment)
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Payments PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        EscrowStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        TransactionRef NVARCHAR(100) NOT NULL, -- Idempotency ref từ VNPAY/MoMo/Bank
        PaymentGateway NVARCHAR(50) NOT NULL DEFAULT 'SystemWallet', -- ⚡ [CẢI TIẾN] VNPAY, MoMo, Wallet
        PaidAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Payments_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id)
    );
END
GO

-- Bảng Ví tiền (Wallet)
IF OBJECT_ID(N'dbo.Wallets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Wallets (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Wallets PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        Balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,        -- Số dư khả dụng
        PendingBalance DECIMAL(18, 2) NOT NULL DEFAULT 0.00, -- ⚡ [CẢI TIẾN] Số dư bị khóa (Escrow hold / Đang rút)
        RowVersion ROWVERSION NOT NULL,                      -- Optimistic concurrency lock
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Wallets_AspNetUsers FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT UX_Wallets_UserId UNIQUE (UserId)
    );
END
GO

-- Bảng Giao dịch (Transaction)
IF OBJECT_ID(N'dbo.Transactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Transactions (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY DEFAULT NEWID(),
        WalletId UNIQUEIDENTIFIER NOT NULL,
        CommissionId UNIQUEIDENTIFIER NULL,
        Type NVARCHAR(50) NOT NULL, -- Deposit, EscrowHold, EscrowRelease, Refund, Payout, PlatformFee, VoucherDiscount
        Amount DECIMAL(18, 2) NOT NULL,
        BalanceAfter DECIMAL(18, 2) NOT NULL DEFAULT 0.00, -- ⚡ [CẢI TIẾN] Số dư sau giao dịch (Audit Ledger)
        Ref NVARCHAR(100) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Transactions_Wallets FOREIGN KEY (WalletId) REFERENCES dbo.Wallets(Id),
        CONSTRAINT FK_Transactions_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id)
    );
END
GO

-- Bảng Yêu cầu Rút tiền (PayoutRequest)
IF OBJECT_ID(N'dbo.PayoutRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PayoutRequests (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PayoutRequests PRIMARY KEY DEFAULT NEWID(),
        WalletId UNIQUEIDENTIFIER NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected, Completed
        BankInfo NVARCHAR(MAX) NOT NULL,                -- JSON bank details (STK, Ngân hàng, Tên chủ TK)
        AdminNote NVARCHAR(1000) NULL,                  -- ⚡ [CẢI TIẾN] Ghi chú duyệt/từ chối của Admin
        RequestedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        ProcessedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_PayoutRequests_Wallets FOREIGN KEY (WalletId) REFERENCES dbo.Wallets(Id)
    );
END
GO

-- ============================================================================
-- 6. CHAT & NOTIFICATION MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Tin nhắn Workroom (Message)
IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messages (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Messages PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        SenderId UNIQUEIDENTIFIER NOT NULL,
        MessageType NVARCHAR(50) NOT NULL DEFAULT 'Text', -- ⚡ [CẢI TIẾN] Text, Image, File, SystemEvent, RevisionNotice
        Body NVARCHAR(MAX) NOT NULL,
        AttachmentUrl NVARCHAR(500) NULL,                 -- ⚡ [CẢI TIẾN] Link file/ảnh đính kèm trong chat
        SentAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        IsRead BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Messages_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Messages_Sender FOREIGN KEY (SenderId) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_Messages_Commission_SentAt ON dbo.Messages(CommissionId, SentAt);
END
GO

-- Bảng Thông báo (Notification)
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Message NVARCHAR(1000) NOT NULL,
        TargetUrl NVARCHAR(500) NULL,                     -- ⚡ [CẢI TIẾN] Deep-link đến Workroom/Event/Order
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Notifications_AspNetUsers FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_Notifications_User_IsRead ON dbo.Notifications(UserId, IsRead);
END
GO

-- ============================================================================
-- 7. AUCTION MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Phiên Đấu giá Tranh
IF OBJECT_ID(N'dbo.AuctionListings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuctionListings (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuctionListings PRIMARY KEY DEFAULT NEWID(),
        ArtworkId UNIQUEIDENTIFIER NOT NULL,
        SellerId UNIQUEIDENTIFIER NOT NULL,
        StartingPrice DECIMAL(18, 2) NOT NULL,
        ReservePrice DECIMAL(18, 2) NULL,             -- ⚡ [CẢI TIẾN] Giá sàn tối thiểu để chốt bán
        BidIncrement DECIMAL(18, 2) NOT NULL DEFAULT 10000.00,
        BuyNowPrice DECIMAL(18, 2) NULL,
        StartAt DATETIMEOFFSET NOT NULL,
        EndsAt DATETIMEOFFSET NOT NULL,
        AutoExtendMinutes INT NOT NULL DEFAULT 5,      -- ⚡ [CẢI TIẾN] Tự gia hạn nếu có bid ở phút cuối (Anti-sniping)
        Status NVARCHAR(50) NOT NULL DEFAULT 'Scheduled', -- Scheduled, Live, Ended, Cancelled
        WinnerId UNIQUEIDENTIFIER NULL,
        FinalPrice DECIMAL(18, 2) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_AuctionListings_Artwork FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks(Id),
        CONSTRAINT FK_AuctionListings_Seller FOREIGN KEY (SellerId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_AuctionListings_Winner FOREIGN KEY (WinnerId) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_AuctionListings_Status_EndsAt ON dbo.AuctionListings(Status, EndsAt);
END
GO

-- Bảng Lượt Đặt giá (Bid)
IF OBJECT_ID(N'dbo.AuctionBids', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuctionBids (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuctionBids PRIMARY KEY DEFAULT NEWID(),
        AuctionListingId UNIQUEIDENTIFIER NOT NULL,
        BidderId UNIQUEIDENTIFIER NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        PlacedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_AuctionBids_AuctionListing FOREIGN KEY (AuctionListingId) REFERENCES dbo.AuctionListings(Id) ON DELETE CASCADE,
        CONSTRAINT FK_AuctionBids_Bidder FOREIGN KEY (BidderId) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_AuctionBids_Listing_Amount ON dbo.AuctionBids(AuctionListingId, Amount DESC);
END
GO

-- ============================================================================
-- 9. ARTIST TEAM MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Nhóm Họa sĩ (ArtistTeam)
IF OBJECT_ID(N'dbo.ArtistTeams', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtistTeams (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ArtistTeams PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(1000) NULL,
        OwnerId UNIQUEIDENTIFIER NOT NULL,
        AvatarUrl NVARCHAR(500) NULL,
        MemberCount INT NOT NULL DEFAULT 1,           -- ⚡ [CẢI TIẾN] Thống kê số thành viên nhóm
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_ArtistTeams_Owner FOREIGN KEY (OwnerId) REFERENCES dbo.AspNetUsers(Id)
    );
END
GO

-- Bảng Thành viên Nhóm Họa sĩ
IF OBJECT_ID(N'dbo.ArtistTeamMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtistTeamMembers (
        TeamId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'Member', -- Owner, Moderator, Member
        JoinedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_ArtistTeamMembers PRIMARY KEY (TeamId, UserId),
        CONSTRAINT FK_ArtistTeamMembers_Team FOREIGN KEY (TeamId) REFERENCES dbo.ArtistTeams(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ArtistTeamMembers_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id)
    );
END
GO

-- Bảng Chat Nội bộ Nhóm Họa sĩ
IF OBJECT_ID(N'dbo.TeamMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TeamMessages (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TeamMessages PRIMARY KEY DEFAULT NEWID(),
        TeamId UNIQUEIDENTIFIER NOT NULL,
        SenderId UNIQUEIDENTIFIER NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        AttachmentUrl NVARCHAR(500) NULL,             -- ⚡ [CẢI TIẾN] Gửi ảnh/file đính kèm trong nhóm
        SentAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_TeamMessages_Team FOREIGN KEY (TeamId) REFERENCES dbo.ArtistTeams(Id) ON DELETE CASCADE,
        CONSTRAINT FK_TeamMessages_Sender FOREIGN KEY (SenderId) REFERENCES dbo.AspNetUsers(Id)
    );
END
GO

-- ============================================================================
-- 10. EVENT & CONTEST MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Sự kiện / Cuộc thi
IF OBJECT_ID(N'dbo.PlatformEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformEvents (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlatformEvents PRIMARY KEY DEFAULT NEWID(),
        Title NVARCHAR(200) NOT NULL,
        BannerUrl NVARCHAR(500) NULL,                 -- ⚡ [CẢI TIẾN] Ảnh Banner cuộc thi
        Description NVARCHAR(MAX) NOT NULL,
        Rules NVARCHAR(MAX) NULL,
        Prize NVARCHAR(1000) NULL,
        MaxVote INT NOT NULL DEFAULT 1,               -- ⚡ Số lượt bình chọn tối đa của mỗi người dùng
        Status NVARCHAR(50) NOT NULL DEFAULT 'Draft', -- Draft, Open, Judging, Ended
        IsHeadJurySignedOff BIT NOT NULL DEFAULT 0,   -- ⚡ Trưởng ban giám khảo đã ký duyệt kết quả
        HeadJurySignedOffAt DATETIMEOFFSET NULL,      -- ⚡ Thời điểm Head Jury ký duyệt
        SubmissionStartAt DATETIMEOFFSET NOT NULL,
        SubmissionEndAt DATETIMEOFFSET NOT NULL,
        JudgingStartAt DATETIMEOFFSET NOT NULL,
        JudgingEndAt DATETIMEOFFSET NOT NULL,
        VotingStartAt DATETIMEOFFSET NOT NULL,
        VotingEndAt DATETIMEOFFSET NOT NULL,
        ResultAnnouncementAt DATETIMEOFFSET NOT NULL,
        CreatedByAdminId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_PlatformEvents_Admin FOREIGN KEY (CreatedByAdminId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT CK_PlatformEvents_Timeline CHECK (
            SubmissionStartAt < SubmissionEndAt
            AND SubmissionEndAt < JudgingStartAt
            AND JudgingStartAt < JudgingEndAt
            AND JudgingEndAt < VotingStartAt
            AND VotingStartAt < VotingEndAt
            AND VotingEndAt < ResultAnnouncementAt
        )
    );

    CREATE INDEX IX_PlatformEvents_Status_SubmissionTimeline ON dbo.PlatformEvents(Status, SubmissionStartAt, SubmissionEndAt);
END
GO

-- Bảng Lời mời Ban giám khảo / Sự kiện (Invitation)
IF OBJECT_ID(N'dbo.Invitations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invitations (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Invitations PRIMARY KEY DEFAULT NEWID(),
        EventId UNIQUEIDENTIFIER NOT NULL,
        SentFromAdminId UNIQUEIDENTIFIER NOT NULL,
        SentToCreatorId UNIQUEIDENTIFIER NOT NULL,
        IsHeadJury BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        RespondedAt DATETIMEOFFSET NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', 
        CONSTRAINT FK_Invitations_Event FOREIGN KEY (EventId) REFERENCES dbo.PlatformEvents(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Invitations_SentFromAdmin FOREIGN KEY (SentFromAdminId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_Invitations_SentToCreator FOREIGN KEY (SentToCreatorId) REFERENCES dbo.CreatorProfiles(Id),
        CONSTRAINT CK_Invitations_Status CHECK (Status IN ('Pending', 'Accepted', 'Declined', 'Canceled', 'Expired')),
        CONSTRAINT CK_Invitations_RespondedAt CHECK (RespondedAt IS NULL OR RespondedAt >= CreatedAt)
    );

    CREATE INDEX IX_Invitations_Event_Status ON dbo.Invitations(EventId, Status);
    CREATE INDEX IX_Invitations_SentToCreator_Status ON dbo.Invitations(SentToCreatorId, Status);
    CREATE INDEX IX_Invitations_SentFromAdmin ON dbo.Invitations(SentFromAdminId);
    CREATE UNIQUE INDEX UX_Invitations_Event_Creator_Pending ON dbo.Invitations(EventId, SentToCreatorId) WHERE Status = 'Pending';
END
GO

-- Bảng Ban giám khảo Sự kiện (Jury)
IF OBJECT_ID(N'dbo.Juries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Juries (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Juries PRIMARY KEY DEFAULT NEWID(),
        EventId UNIQUEIDENTIFIER NOT NULL,
        CreatorId UNIQUEIDENTIFIER NOT NULL,
        IsHeadJury BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Juries_Event FOREIGN KEY (EventId) REFERENCES dbo.PlatformEvents(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Juries_Creator FOREIGN KEY (CreatorId) REFERENCES dbo.CreatorProfiles(Id),
        CONSTRAINT UX_Juries_Event_Creator UNIQUE (EventId, CreatorId)
    );

    CREATE INDEX IX_Juries_Creator ON dbo.Juries(CreatorId);
    CREATE UNIQUE INDEX UX_Juries_Event_HeadJury ON dbo.Juries(EventId) WHERE IsHeadJury = 1;
END
GO

-- Hỗ trợ truy vấn linh hoạt theo tên số ít (dbo.Invitation & dbo.Jury)
IF NOT EXISTS (SELECT * FROM sys.synonyms WHERE name = N'Invitation' AND schema_id = SCHEMA_ID(N'dbo'))
    CREATE SYNONYM dbo.Invitation FOR dbo.Invitations;
GO

IF NOT EXISTS (SELECT * FROM sys.synonyms WHERE name = N'Jury' AND schema_id = SCHEMA_ID(N'dbo'))
    CREATE SYNONYM dbo.Jury FOR dbo.Juries;
GO

-- Bảng Tiêu chí Chấm điểm Sự kiện (EventCriteria)
IF OBJECT_ID(N'dbo.EventCriteria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventCriteria (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EventCriteria PRIMARY KEY DEFAULT NEWID(),
        EventId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(150) NOT NULL,            -- Tên tiêu chí (VD: Bố cục, Màu sắc)
        Description NVARCHAR(500) NULL,
        MaxScore DECIMAL(5, 2) NOT NULL DEFAULT 10.00, -- Thang điểm tối đa (VD: 10)
        Weight DECIMAL(5, 2) NOT NULL,          -- Trọng số (VD: 0.4 tương ứng 40%)
        DisplayOrder INT NOT NULL DEFAULT 0,    -- Thứ tự hiển thị
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_EventCriteria_Event FOREIGN KEY (EventId) REFERENCES dbo.PlatformEvents(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_EventCriteria_EventId ON dbo.EventCriteria(EventId);
END
GO

-- Bảng Bài nộp dự thi
IF OBJECT_ID(N'dbo.EventSubmissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventSubmissions (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EventSubmissions PRIMARY KEY DEFAULT NEWID(),
        EventId UNIQUEIDENTIFIER NOT NULL,
        SubmitterId UNIQUEIDENTIFIER NOT NULL,
        ArtworkId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL DEFAULT '',
        Description NVARCHAR(2000) NULL,
        AiScanPassed BIT NOT NULL DEFAULT 0,
        VoteCount INT NOT NULL DEFAULT 0,              -- ⚡ [CẢI TIẾN] Đếm tổng lượt vote (Leaderboard O(1))
        Score DECIMAL(5, 2) NULL,                     -- Điểm BGK chấm
        AdminNote NVARCHAR(1000) NULL,                -- ⚡ [CẢI TIẾN] Nhận xét của BGK
        SubmittedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_EventSubmissions_Event FOREIGN KEY (EventId) REFERENCES dbo.PlatformEvents(Id) ON DELETE CASCADE,
        CONSTRAINT FK_EventSubmissions_Submitter FOREIGN KEY (SubmitterId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_EventSubmissions_Artwork FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks(Id)
    );

    CREATE INDEX IX_EventSubmissions_Event_Score ON dbo.EventSubmissions(EventId, Score DESC, VoteCount DESC);
END
GO

-- Bảng Điểm Giám khảo theo Tiêu chí (CriteriaScores)
IF OBJECT_ID(N'dbo.CriteriaScores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CriteriaScores (
        EventCriteriaId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        GradedByJuryId UNIQUEIDENTIFIER NOT NULL,   -- ID của giám khảo trong bảng Juries
        Score DECIMAL(5, 2) NOT NULL,               -- Điểm giám khảo cho tiêu chí này
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_CriteriaScores PRIMARY KEY (EventCriteriaId, SubmissionId, GradedByJuryId),
        CONSTRAINT FK_CriteriaScores_Criteria FOREIGN KEY (EventCriteriaId) REFERENCES dbo.EventCriteria(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CriteriaScores_Submission FOREIGN KEY (SubmissionId) REFERENCES dbo.EventSubmissions(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CriteriaScores_Jury FOREIGN KEY (GradedByJuryId) REFERENCES dbo.Juries(Id)
    );

    CREATE INDEX IX_CriteriaScores_SubmissionId ON dbo.CriteriaScores(SubmissionId);
    CREATE INDEX IX_CriteriaScores_GradedByJuryId ON dbo.CriteriaScores(GradedByJuryId);
END
GO

-- Bảng Bình chọn Bài dự thi (Vote)
IF OBJECT_ID(N'dbo.EventVotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventVotes (
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        VoterId UNIQUEIDENTIFIER NOT NULL,
        VotedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_EventVotes PRIMARY KEY (SubmissionId, VoterId),
        CONSTRAINT FK_EventVotes_Submission FOREIGN KEY (SubmissionId) REFERENCES dbo.EventSubmissions(Id) ON DELETE CASCADE,
        CONSTRAINT FK_EventVotes_Voter FOREIGN KEY (VoterId) REFERENCES dbo.AspNetUsers(Id)
    );
END
GO

-- Bảng Khiếu nại Bài thi Sự kiện (SubmissionDispute / SubmissionDisputes)
IF OBJECT_ID(N'dbo.SubmissionDisputes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubmissionDisputes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionDisputes PRIMARY KEY DEFAULT NEWID(),
        EventId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        RaisedById UNIQUEIDENTIFIER NOT NULL,
        Priority NVARCHAR(50) NOT NULL DEFAULT 'Medium', -- Low, Medium, High
        Reason NVARCHAR(2000) NOT NULL,
        EvidenceUrls NVARCHAR(MAX) NULL,               -- JSON array ảnh/link bằng chứng khiếu nại
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, UnderReview, Resolved, Rejected
        Resolution NVARCHAR(50) NULL,                 -- Dismiss, Disqualify, RequestReEvaluation, Other
        Note NVARCHAR(MAX) NULL,                      -- Ghi chú phân xử của Admin / Moderator
        ResolvedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_SubmissionDisputes_Event FOREIGN KEY (EventId) REFERENCES dbo.PlatformEvents(Id),
        CONSTRAINT FK_SubmissionDisputes_Submission FOREIGN KEY (SubmissionId) REFERENCES dbo.EventSubmissions(Id) ON DELETE CASCADE,
        CONSTRAINT FK_SubmissionDisputes_RaisedBy FOREIGN KEY (RaisedById) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_SubmissionDisputes_SubmissionId ON dbo.SubmissionDisputes(SubmissionId);
    CREATE INDEX IX_SubmissionDisputes_Event_Status ON dbo.SubmissionDisputes(EventId, Status);
END
GO

-- Bảng Huy hiệu / Badge Hệ thống
IF OBJECT_ID(N'dbo.Badges', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Badges (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Badges PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(100) NOT NULL,
        IconUrl NVARCHAR(500) NOT NULL,
        Description NVARCHAR(500) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
END
GO

-- Bảng Huy hiệu Người dùng Sở hữu
IF OBJECT_ID(N'dbo.UserBadges', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserBadges (
        UserId UNIQUEIDENTIFIER NOT NULL,
        BadgeId UNIQUEIDENTIFIER NOT NULL,
        AwardedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Reason NVARCHAR(500) NULL,
        CONSTRAINT PK_UserBadges PRIMARY KEY (UserId, BadgeId),
        CONSTRAINT FK_UserBadges_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserBadges_Badge FOREIGN KEY (BadgeId) REFERENCES dbo.Badges(Id) ON DELETE CASCADE
    );
END
GO

-- ============================================================================
-- 11. VOUCHER USAGE MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Lịch sử Sử dụng Voucher
IF OBJECT_ID(N'dbo.VoucherUsages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VoucherUsages (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_VoucherUsages PRIMARY KEY DEFAULT NEWID(),
        VoucherId UNIQUEIDENTIFIER NOT NULL,
        UsedById UNIQUEIDENTIFIER NOT NULL,
        CommissionId UNIQUEIDENTIFIER NULL,
        UsedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_VoucherUsages_Voucher FOREIGN KEY (VoucherId) REFERENCES dbo.Vouchers(Id),
        CONSTRAINT FK_VoucherUsages_UsedBy FOREIGN KEY (UsedById) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_VoucherUsages_Commission FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id)
    );
END
GO

-- ============================================================================
-- 12. CREATOR APPLICATION MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Đơn đăng ký làm Creator
IF OBJECT_ID(N'dbo.CreatorApplications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CreatorApplications (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CreatorApplications PRIMARY KEY DEFAULT NEWID(),
        ApplicantId UNIQUEIDENTIFIER NOT NULL,
        PortfolioLinks NVARCHAR(MAX) NOT NULL, -- JSON Array các link portfolio (ArtStation, Behance...)
        SocialLinks NVARCHAR(MAX) NULL,       -- ⚡ [CẢI TIẾN] JSON array mạng xã hội
        IdProofUrl NVARCHAR(500) NOT NULL,     -- Ảnh CCCD/CMND xác thực identity
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected
        ReviewedByModId UNIQUEIDENTIFIER NULL,
        ReviewNote NVARCHAR(1000) NULL,
        SubmittedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        ReviewedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CreatorApplications_Applicant FOREIGN KEY (ApplicantId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_CreatorApplications_ReviewedBy FOREIGN KEY (ReviewedByModId) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_CreatorApplications_Applicant_Status ON dbo.CreatorApplications(ApplicantId, Status);
END
GO

-- ============================================================================
-- 13. BOOKMARK & COLLECTION MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Bộ sưu tập Tranh (Collection)
IF OBJECT_ID(N'dbo.Collections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Collections (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Collections PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        IsPrivate BIT NOT NULL DEFAULT 0,
        ItemCount INT NOT NULL DEFAULT 0,             -- ⚡ [CẢI TIẾN] Đếm tổng số tranh trong collection
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Collections_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );
END
GO

-- Bảng Chi tiết Bài tranh trong Bộ sưu tập
IF OBJECT_ID(N'dbo.CollectionItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CollectionItems (
        CollectionId UNIQUEIDENTIFIER NOT NULL,
        ArtworkId UNIQUEIDENTIFIER NOT NULL,
        AddedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_CollectionItems PRIMARY KEY (CollectionId, ArtworkId),
        CONSTRAINT FK_CollectionItems_Collection FOREIGN KEY (CollectionId) REFERENCES dbo.Collections(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CollectionItems_Artwork FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks(Id) ON DELETE CASCADE
    );
END
GO

-- ============================================================================
-- 14. PLATFORM CONFIGURATION MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Cấu hình Hệ thống & Phí Sàn
IF OBJECT_ID(N'dbo.PlatformSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformSettings (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlatformSettings PRIMARY KEY DEFAULT NEWID(),
        [Key] NVARCHAR(100) NOT NULL,
        Value NVARCHAR(MAX) NOT NULL,
        Description NVARCHAR(500) NULL,
        UpdatedByAdminId UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_PlatformSettings_Admin FOREIGN KEY (UpdatedByAdminId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT UX_PlatformSettings_Key UNIQUE ([Key])
    );
END
GO

-- ============================================================================
-- 15. ART TRADE & TRADE DISPUTES MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Trao đổi tranh (Art Trade) giữa 2 nghệ sĩ
IF OBJECT_ID(N'dbo.ArtTrades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtTrades (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ArtTrades PRIMARY KEY DEFAULT NEWID(),
        InitiatorId UNIQUEIDENTIFIER NOT NULL,
        ReceiverId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        InitiatorRequestNote NVARCHAR(2000) NULL,
        ReceiverRequestNote NVARCHAR(2000) NULL,
        ReceiverResponseNote NVARCHAR(2000) NULL,
        ReceiverAcceptedAt DATETIMEOFFSET NULL,
        InitiatorArtworkId UNIQUEIDENTIFIER NULL,
        ReceiverArtworkId UNIQUEIDENTIFIER NULL,
        InitiatorOriginalFileUrl NVARCHAR(500) NULL,
        InitiatorPreviewUrl NVARCHAR(500) NULL,
        InitiatorSubmittedAt DATETIMEOFFSET NULL,
        ReceiverOriginalFileUrl NVARCHAR(500) NULL,
        ReceiverPreviewUrl NVARCHAR(500) NULL,
        ReceiverSubmittedAt DATETIMEOFFSET NULL,
        IsInitiatorConfirmed BIT NOT NULL DEFAULT 0,
        InitiatorConfirmedAt DATETIMEOFFSET NULL,
        IsReceiverConfirmed BIT NOT NULL DEFAULT 0,
        ReceiverConfirmedAt DATETIMEOFFSET NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Proposed', -- Proposed, Accepted, BothSubmitted, Completed, Rejected, Cancelled, Disputed
        SubmissionDeadlineAt DATETIMEOFFSET NULL,
        ConfirmationDeadlineAt DATETIMEOFFSET NULL,
        CompletedAt DATETIMEOFFSET NULL,
        CancelledAt DATETIMEOFFSET NULL,
        CancelledById UNIQUEIDENTIFIER NULL,
        CancellationReason NVARCHAR(500) NULL,
        RejectionReason NVARCHAR(500) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_ArtTrades_InitiatorArtwork FOREIGN KEY (InitiatorArtworkId) REFERENCES dbo.Artworks(Id),
        CONSTRAINT FK_ArtTrades_ReceiverArtwork FOREIGN KEY (ReceiverArtworkId) REFERENCES dbo.Artworks(Id)
    );

    CREATE INDEX IX_ArtTrades_InitiatorId_CreatedAt ON dbo.ArtTrades(InitiatorId, CreatedAt DESC);
    CREATE INDEX IX_ArtTrades_ReceiverId_CreatedAt ON dbo.ArtTrades(ReceiverId, CreatedAt DESC);
    CREATE INDEX IX_ArtTrades_Status_UpdatedAt ON dbo.ArtTrades(Status, UpdatedAt DESC);
    CREATE INDEX IX_ArtTrades_InitiatorArtworkId ON dbo.ArtTrades(InitiatorArtworkId);
    CREATE INDEX IX_ArtTrades_ReceiverArtworkId ON dbo.ArtTrades(ReceiverArtworkId);
    CREATE INDEX IX_ArtTrades_CancelledById ON dbo.ArtTrades(CancelledById);
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.ArtTrades', N'InitiatorRequestNote') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD InitiatorRequestNote NVARCHAR(2000) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverRequestNote') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverRequestNote NVARCHAR(2000) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverResponseNote') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverResponseNote NVARCHAR(2000) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverAcceptedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverAcceptedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'InitiatorArtworkId') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD InitiatorArtworkId UNIQUEIDENTIFIER NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverArtworkId') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverArtworkId UNIQUEIDENTIFIER NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'InitiatorOriginalFileUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD InitiatorOriginalFileUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'InitiatorPreviewUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD InitiatorPreviewUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'InitiatorSubmittedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD InitiatorSubmittedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverOriginalFileUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverOriginalFileUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverPreviewUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverPreviewUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverSubmittedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverSubmittedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'IsInitiatorConfirmed') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD IsInitiatorConfirmed BIT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'InitiatorConfirmedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD InitiatorConfirmedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'IsReceiverConfirmed') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD IsReceiverConfirmed BIT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ReceiverConfirmedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ReceiverConfirmedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'Status') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD Status NVARCHAR(50) NOT NULL DEFAULT ''Proposed'';');
    IF COL_LENGTH(N'dbo.ArtTrades', N'SubmissionDeadlineAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD SubmissionDeadlineAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'ConfirmationDeadlineAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD ConfirmationDeadlineAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'CompletedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CompletedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'CancelledAt') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CancelledAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'CancelledById') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CancelledById UNIQUEIDENTIFIER NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'CancellationReason') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CancellationReason NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'RejectionReason') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD RejectionReason NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.ArtTrades', N'IsDeleted') IS NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD IsDeleted BIT NOT NULL DEFAULT 0;');
END
GO

-- Ràng buộc khoá ngoại an toàn cho ArtTrades
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ArtTrades_Initiator')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CONSTRAINT FK_ArtTrades_Initiator FOREIGN KEY (InitiatorId) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CONSTRAINT FK_ArtTrades_Initiator FOREIGN KEY (InitiatorId) REFERENCES dbo.AspNetUsers(Id);');
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ArtTrades_Receiver')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CONSTRAINT FK_ArtTrades_Receiver FOREIGN KEY (ReceiverId) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CONSTRAINT FK_ArtTrades_Receiver FOREIGN KEY (ReceiverId) REFERENCES dbo.AspNetUsers(Id);');
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ArtTrades_CancelledBy')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CONSTRAINT FK_ArtTrades_CancelledBy FOREIGN KEY (CancelledById) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtTrades ADD CONSTRAINT FK_ArtTrades_CancelledBy FOREIGN KEY (CancelledById) REFERENCES dbo.AspNetUsers(Id);');
END;
GO

-- Bảng Đóng băng slot trao đổi tranh (Trade Slot Freeze)
IF OBJECT_ID(N'dbo.TradeSlotFreezes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradeSlotFreezes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TradeSlotFreezes PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        ArtTradeId UNIQUEIDENTIFIER NOT NULL,
        FrozenFrom DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        FrozenUntil DATETIMEOFFSET NOT NULL,
        Reason NVARCHAR(500) NULL,
        IsRevoked BIT NOT NULL DEFAULT 0,
        RevokedReason NVARCHAR(500) NULL,
        RevokedAt DATETIMEOFFSET NULL,
        RevokedByAdminId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_TradeSlotFreezes_ArtTrade FOREIGN KEY (ArtTradeId) REFERENCES dbo.ArtTrades(Id)
    );

    CREATE INDEX IX_TradeSlotFreezes_User_Active ON dbo.TradeSlotFreezes(UserId, FrozenUntil, IsRevoked, IsDeleted);
    CREATE INDEX IX_TradeSlotFreezes_ArtTradeId ON dbo.TradeSlotFreezes(ArtTradeId);
    CREATE INDEX IX_TradeSlotFreezes_RevokedByAdminId ON dbo.TradeSlotFreezes(RevokedByAdminId);
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.TradeSlotFreezes', N'Reason') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD Reason NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.TradeSlotFreezes', N'IsRevoked') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD IsRevoked BIT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.TradeSlotFreezes', N'RevokedReason') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD RevokedReason NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.TradeSlotFreezes', N'RevokedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD RevokedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.TradeSlotFreezes', N'RevokedByAdminId') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD RevokedByAdminId UNIQUEIDENTIFIER NULL;');
    IF COL_LENGTH(N'dbo.TradeSlotFreezes', N'IsDeleted') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD IsDeleted BIT NOT NULL DEFAULT 0;');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TradeSlotFreezes_User')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD CONSTRAINT FK_TradeSlotFreezes_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD CONSTRAINT FK_TradeSlotFreezes_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id);');
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TradeSlotFreezes_RevokedByAdmin')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD CONSTRAINT FK_TradeSlotFreezes_RevokedByAdmin FOREIGN KEY (RevokedByAdminId) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.TradeSlotFreezes ADD CONSTRAINT FK_TradeSlotFreezes_RevokedByAdmin FOREIGN KEY (RevokedByAdminId) REFERENCES dbo.AspNetUsers(Id);');
END;
GO

-- Bảng Khiếu nại tranh chấp trong trao đổi tranh (Trade Disputes)
IF OBJECT_ID(N'dbo.TradeDisputes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradeDisputes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TradeDisputes PRIMARY KEY DEFAULT NEWID(),
        ArtTradeId UNIQUEIDENTIFIER NOT NULL,
        RaisedById UNIQUEIDENTIFIER NOT NULL,
        Reason NVARCHAR(2000) NOT NULL,
        EvidenceUrls NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        Resolution NVARCHAR(50) NULL,
        AdminNote NVARCHAR(2000) NULL,
        ResolvedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_TradeDisputes_ArtTrade FOREIGN KEY (ArtTradeId) REFERENCES dbo.ArtTrades(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_TradeDisputes_ArtTradeId ON dbo.TradeDisputes(ArtTradeId);
    CREATE INDEX IX_TradeDisputes_RaisedById ON dbo.TradeDisputes(RaisedById);
    CREATE INDEX IX_TradeDisputes_Status ON dbo.TradeDisputes(Status);
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.TradeDisputes', N'EvidenceUrls') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD EvidenceUrls NVARCHAR(MAX) NULL;');
    IF COL_LENGTH(N'dbo.TradeDisputes', N'Resolution') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD Resolution NVARCHAR(50) NULL;');
    IF COL_LENGTH(N'dbo.TradeDisputes', N'AdminNote') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD AdminNote NVARCHAR(2000) NULL;');
    IF COL_LENGTH(N'dbo.TradeDisputes', N'ResolvedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD ResolvedAt DATETIMEOFFSET NULL;');
    IF COL_LENGTH(N'dbo.TradeDisputes', N'IsDeleted') IS NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD IsDeleted BIT NOT NULL DEFAULT 0;');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TradeDisputes_RaisedBy')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD CONSTRAINT FK_TradeDisputes_RaisedBy FOREIGN KEY (RaisedById) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.TradeDisputes ADD CONSTRAINT FK_TradeDisputes_RaisedBy FOREIGN KEY (RaisedById) REFERENCES dbo.AspNetUsers(Id);');
END;
GO

-- Bảng Quyền sở hữu Tác phẩm (Artwork Ownerships)
IF OBJECT_ID(N'dbo.ArtworkOwnerships', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtworkOwnerships (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ArtworkOwnerships PRIMARY KEY DEFAULT NEWID(),
        ArtworkId UNIQUEIDENTIFIER NOT NULL,
        OwnerId UNIQUEIDENTIFIER NOT NULL,
        AcquiredAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        ReleasedAt DATETIMEOFFSET NULL,
        TransferReason NVARCHAR(30) NOT NULL DEFAULT 'AuctionWin',
        AuctionId UNIQUEIDENTIFIER NULL,
        CommissionId UNIQUEIDENTIFIER NULL,
        ArtTradeId UNIQUEIDENTIFIER NULL,
        IsCurrent BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_ArtworkOwnerships_Artworks FOREIGN KEY (ArtworkId) REFERENCES dbo.Artworks(Id),
        CONSTRAINT FK_ArtworkOwnerships_ArtTrades FOREIGN KEY (ArtTradeId) REFERENCES dbo.ArtTrades(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_ArtworkOwnerships_ArtworkId ON dbo.ArtworkOwnerships(ArtworkId);
    CREATE INDEX IX_ArtworkOwnerships_Owner_Current ON dbo.ArtworkOwnerships(OwnerId, IsCurrent);
    CREATE INDEX IX_ArtworkOwnerships_ArtTradeId ON dbo.ArtworkOwnerships(ArtTradeId);
    CREATE UNIQUE INDEX UX_ArtworkOwnerships_CurrentOwner ON dbo.ArtworkOwnerships(ArtworkId) WHERE [IsCurrent] = 1;
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.ArtworkOwnerships', N'ArtTradeId') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.ArtworkOwnerships ADD ArtTradeId UNIQUEIDENTIFIER NULL;');
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name IN ('FK_ArtworkOwnerships_ArtTrade', 'FK_ArtworkOwnerships_ArtTrades'))
    BEGIN
        EXEC(N'ALTER TABLE dbo.ArtworkOwnerships ADD CONSTRAINT FK_ArtworkOwnerships_ArtTrades FOREIGN KEY (ArtTradeId) REFERENCES dbo.ArtTrades(Id) ON DELETE SET NULL;');
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ArtworkOwnerships_ArtTradeId')
    BEGIN
        EXEC(N'CREATE INDEX IX_ArtworkOwnerships_ArtTradeId ON dbo.ArtworkOwnerships(ArtTradeId);');
    END;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ArtworkOwnerships_Users')
BEGIN
    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtworkOwnerships ADD CONSTRAINT FK_ArtworkOwnerships_Users FOREIGN KEY (OwnerId) REFERENCES dbo.Users(Id);');
    ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.ArtworkOwnerships ADD CONSTRAINT FK_ArtworkOwnerships_Users FOREIGN KEY (OwnerId) REFERENCES dbo.AspNetUsers(Id);');
END;
GO

-- Đồng bộ bổ sung các cột còn thiếu cho CreatorProfiles nếu bảng đã có sẵn
IF OBJECT_ID(N'dbo.CreatorProfiles', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'Headline') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD Headline NVARCHAR(200) NULL;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'Specialties') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD Specialties NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'Location') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD Location NVARCHAR(200) NULL;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'WebsiteUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD WebsiteUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'BannerUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD BannerUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'IsAcceptingOrders') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD IsAcceptingOrders BIT NOT NULL DEFAULT 1;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'IsApproved') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD IsApproved BIT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'RatingAverage') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD RatingAverage DECIMAL(18, 2) NOT NULL DEFAULT 0.00;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'RatingCount') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD RatingCount INT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'FollowerCount') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD FollowerCount INT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'AvailableSlots') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD AvailableSlots INT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.CreatorProfiles', N'RateCardJson') IS NULL
        EXEC(N'ALTER TABLE dbo.CreatorProfiles ADD RateCardJson NVARCHAR(MAX) NULL;');
END
GO

-- 16. CREATOR & COMMISSION ADDONS MODULE
-- ============================================================================

-- Bảng Gói dịch vụ bổ sung của Creator (Creator Addons)
IF OBJECT_ID(N'dbo.CreatorAddons', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CreatorAddons (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CreatorAddons PRIMARY KEY DEFAULT NEWID(),
        CreatorId UNIQUEIDENTIFIER NOT NULL,
        PackageId UNIQUEIDENTIFIER NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        Price DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        AllowedMilestone NVARCHAR(20) NOT NULL,
        ExtendDays INT NOT NULL DEFAULT 0,
        PresetKey NVARCHAR(60) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CreatorAddons_Creator FOREIGN KEY (CreatorId) REFERENCES dbo.CreatorProfiles(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_CreatorAddons_CreatorId ON dbo.CreatorAddons(CreatorId);
END
GO

-- Bảng Dịch vụ bổ sung được áp dụng vào Commission (Commission Addons)
IF OBJECT_ID(N'dbo.CommissionAddons', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommissionAddons (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommissionAddons PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        ProposedById UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        Price DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        AllowedMilestone NVARCHAR(20) NOT NULL,
        MilestoneSequence INT NOT NULL DEFAULT 1,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        ExtendDays INT NOT NULL DEFAULT 0,
        IsPreset BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CommissionAddons_Commission FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_CommissionAddons_CommissionId ON dbo.CommissionAddons(CommissionId);
END
GO

-- ============================================================================
-- 17. USER FEEDBACK & BUG REPORTING MODULE
-- ============================================================================

-- Bảng Báo cáo lỗi người dùng (Bug Reports)
IF OBJECT_ID(N'dbo.BugReports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BugReports (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BugReports PRIMARY KEY DEFAULT NEWID(),
        ReporterId UNIQUEIDENTIFIER NULL,
        ReporterEmail NVARCHAR(256) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        PageUrl NVARCHAR(500) NULL,
        Status INT NOT NULL DEFAULT 0, -- 0: Open, 1: InProgress, 2: Resolved, 3: Closed
        AdminNote NVARCHAR(MAX) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_BugReports_Reporter FOREIGN KEY (ReporterId) REFERENCES dbo.AspNetUsers(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_BugReports_Status_CreatedAt ON dbo.BugReports(Status, CreatedAt DESC);
    CREATE INDEX IX_BugReports_ReporterId ON dbo.BugReports(ReporterId);
END
GO

-- Bổ sung cột PackageId cho Commissions (nếu chưa có)
IF OBJECT_ID(N'dbo.Commissions', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Commissions', N'PackageId') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Commissions ADD PackageId UNIQUEIDENTIFIER NULL;');
END
GO

-- Bổ sung các cột AI Scanning cho Artworks (nếu chưa có)
IF OBJECT_ID(N'dbo.Artworks', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanStatus') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanStatus NVARCHAR(50) NULL;');
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanModel') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanModel NVARCHAR(100) NULL;');
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanProvider') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanProvider NVARCHAR(100) NULL;');
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanImageUrl') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanImageUrl NVARCHAR(500) NULL;');
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanError') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanError NVARCHAR(2000) NULL;');
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanAttempts') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanAttempts INT NOT NULL DEFAULT 0;');
    IF COL_LENGTH(N'dbo.Artworks', N'AiScanUpdatedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.Artworks ADD AiScanUpdatedAt DATETIMEOFFSET NULL;');
END
GO

-- ============================================================================
-- INITIAL SEED DATA (Platform Settings Mặc Định)
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.PlatformSettings WHERE [Key] = 'platform_fee_rate')
BEGIN
    INSERT INTO dbo.PlatformSettings (Id, [Key], Value, Description, UpdatedAt)
    VALUES 
    (NEWID(), 'platform_fee_rate', '0.05', N'Tỷ lệ phí sàn áp dụng cho mỗi giao dịch commission (5%)', SYSDATETIMEOFFSET()),
    (NEWID(), 'escrow_hold_days', '7', N'Số ngày tạm giữ tiền escrow sau khi hoàn thành commission', SYSDATETIMEOFFSET()),
    (NEWID(), 'max_revision_count', '3', N'Số lần yêu cầu sửa đổi tối đa mặc định cho 1 milestone', SYSDATETIMEOFFSET());
END
GO

-- ============================================================================
-- EVENT RULES ENFORCEMENT & DATA ADJUSTMENTS
-- ============================================================================

-- 1. Trigger: Creator không được tham gia vào các event nơi mình là Jury (Rule A)
IF OBJECT_ID(N'dbo.TR_EventSubmissions_PreventJurySubmission', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_EventSubmissions_PreventJurySubmission;
GO

CREATE TRIGGER dbo.TR_EventSubmissions_PreventJurySubmission
ON dbo.EventSubmissions
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Tối ưu: Nếu chỉ cập nhật AdminNote, Score, VoteCount mà không đổi SubmitterId/EventId thì bỏ qua
    IF EXISTS (SELECT 1 FROM deleted) AND NOT (UPDATE(SubmitterId) OR UPDATE(EventId))
        RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.CreatorProfiles cp ON i.SubmitterId = cp.UserId
        INNER JOIN dbo.Juries j ON j.EventId = i.EventId AND j.CreatorId = cp.Id
        WHERE j.IsDeleted = 0
    )
    BEGIN
        RAISERROR (N'Thành viên Ban giám khảo (Jury) của sự kiện không được tham gia nộp bài dự thi trong sự kiện đó.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END;
GO

-- 2. Trigger: Creator không được tự vote bài dự thi của chính mình (Rule B)
IF OBJECT_ID(N'dbo.TR_EventVotes_PreventSelfVote', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_EventVotes_PreventSelfVote;
GO

CREATE TRIGGER dbo.TR_EventVotes_PreventSelfVote
ON dbo.EventVotes
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted v
        INNER JOIN dbo.EventSubmissions s ON v.SubmissionId = s.Id
        WHERE v.VoterId = s.SubmitterId
    )
    BEGIN
        RAISERROR (N'Creator (tác giả) không thể tự bình chọn cho bài dự thi của chính mình.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END;
GO

-- 3. Data Adjustments & Cleanup (Đảm bảo dữ liệu hiện có tuân thủ 2 rule mới)
-- Xóa các lượt vote liên quan đến bài dự thi của Jury hoặc tự vote bài của mình
DELETE v
FROM dbo.EventVotes v
INNER JOIN dbo.EventSubmissions s ON v.SubmissionId = s.Id
LEFT JOIN dbo.CreatorProfiles cp ON s.SubmitterId = cp.UserId
LEFT JOIN dbo.Juries j ON j.EventId = s.EventId AND j.CreatorId = cp.Id AND j.IsDeleted = 0
WHERE v.VoterId = s.SubmitterId OR j.Id IS NOT NULL;

-- Xóa điểm tiêu chí (CriteriaScores) của bài dự thi do Jury nộp
IF OBJECT_ID(N'dbo.CriteriaScores', N'U') IS NOT NULL
BEGIN
    DELETE cs
    FROM dbo.CriteriaScores cs
    INNER JOIN dbo.EventSubmissions s ON cs.SubmissionId = s.Id
    INNER JOIN dbo.CreatorProfiles cp ON s.SubmitterId = cp.UserId
    INNER JOIN dbo.Juries j ON j.EventId = s.EventId AND j.CreatorId = cp.Id
    WHERE j.IsDeleted = 0;
END

-- Xóa các bài nộp dự thi nếu tác giả là Jury của sự kiện đó
DELETE s
FROM dbo.EventSubmissions s
INNER JOIN dbo.CreatorProfiles cp ON s.SubmitterId = cp.UserId
INNER JOIN dbo.Juries j ON j.EventId = s.EventId AND j.CreatorId = cp.Id
WHERE j.IsDeleted = 0;

-- Cập nhật lại số lượng VoteCount chính xác cho các bài dự thi còn lại
ALTER TABLE dbo.EventSubmissions DISABLE TRIGGER TR_EventSubmissions_PreventJurySubmission;

UPDATE s
SET s.VoteCount = (
    SELECT COUNT(*) 
    FROM dbo.EventVotes v 
    WHERE v.SubmissionId = s.Id
)
FROM dbo.EventSubmissions s;

ALTER TABLE dbo.EventSubmissions ENABLE TRIGGER TR_EventSubmissions_PreventJurySubmission;
GO

-- Xóa 2 sự kiện The Day You Went Away và Sunlight (kèm toàn bộ dữ liệu phụ thuộc liên quan) nếu có
IF OBJECT_ID(N'dbo.PlatformEvents', N'U') IS NOT NULL
BEGIN
    DECLARE @EventIds TABLE (Id UNIQUEIDENTIFIER);
    INSERT INTO @EventIds (Id)
    SELECT Id FROM dbo.PlatformEvents 
    WHERE Title LIKE N'%The Day You Went Away%' OR Title LIKE N'%Sunlight%';

    IF EXISTS (SELECT 1 FROM @EventIds)
    BEGIN
        IF OBJECT_ID(N'dbo.EventVotes', N'U') IS NOT NULL
            DELETE FROM dbo.EventVotes WHERE SubmissionId IN (SELECT Id FROM dbo.EventSubmissions WHERE EventId IN (SELECT Id FROM @EventIds));

        IF OBJECT_ID(N'dbo.CriteriaScores', N'U') IS NOT NULL
            DELETE FROM dbo.CriteriaScores WHERE EventCriteriaId IN (SELECT Id FROM dbo.EventCriteria WHERE EventId IN (SELECT Id FROM @EventIds))
               OR SubmissionId IN (SELECT Id FROM dbo.EventSubmissions WHERE EventId IN (SELECT Id FROM @EventIds));

        IF OBJECT_ID(N'dbo.SubmissionDisputes', N'U') IS NOT NULL
            DELETE FROM dbo.SubmissionDisputes WHERE EventId IN (SELECT Id FROM @EventIds);

        IF OBJECT_ID(N'dbo.EventSubmissions', N'U') IS NOT NULL
            DELETE FROM dbo.EventSubmissions WHERE EventId IN (SELECT Id FROM @EventIds);

        IF OBJECT_ID(N'dbo.EventCriteria', N'U') IS NOT NULL
            DELETE FROM dbo.EventCriteria WHERE EventId IN (SELECT Id FROM @EventIds);

        IF OBJECT_ID(N'dbo.Juries', N'U') IS NOT NULL
            DELETE FROM dbo.Juries WHERE EventId IN (SELECT Id FROM @EventIds);

        IF OBJECT_ID(N'dbo.Invitations', N'U') IS NOT NULL
            DELETE FROM dbo.Invitations WHERE EventId IN (SELECT Id FROM @EventIds);

        DELETE FROM dbo.PlatformEvents WHERE Id IN (SELECT Id FROM @EventIds);
    END
END
GO

-- 4. Seed / Ensure Creator Profile & Role for artist1@seed.dillustration.art
DECLARE @ArtistEmail NVARCHAR(256) = N'artist1@seed.dillustration.art';
DECLARE @ArtistUserId UNIQUEIDENTIFIER = NULL;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    SELECT TOP 1 @ArtistUserId = Id FROM dbo.Users WHERE Email = @ArtistEmail;
ELSE IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
    SELECT TOP 1 @ArtistUserId = Id FROM dbo.AspNetUsers WHERE Email = @ArtistEmail;

IF @ArtistUserId IS NOT NULL
BEGIN
    -- Ensure Creator Role
    DECLARE @CreatorRoleId UNIQUEIDENTIFIER = NULL;
    IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
        SELECT TOP 1 @CreatorRoleId = Id FROM dbo.Roles WHERE Name = N'Creator';
    ELSE IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NOT NULL
        SELECT TOP 1 @CreatorRoleId = Id FROM dbo.AspNetRoles WHERE Name = N'Creator';

    IF @CreatorRoleId IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL
        BEGIN
            EXEC sp_executesql N'
                IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @uid AND RoleId = @rid)
                    INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@uid, @rid);',
                N'@uid UNIQUEIDENTIFIER, @rid UNIQUEIDENTIFIER', @uid = @ArtistUserId, @rid = @CreatorRoleId;
        END
        ELSE IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NOT NULL
        BEGIN
            EXEC sp_executesql N'
                IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = @uid AND RoleId = @rid)
                    INSERT INTO dbo.AspNetUserRoles (UserId, RoleId) VALUES (@uid, @rid);',
                N'@uid UNIQUEIDENTIFIER, @rid UNIQUEIDENTIFIER', @uid = @ArtistUserId, @rid = @CreatorRoleId;
        END
    END

    -- Ensure CreatorProfile
    IF OBJECT_ID(N'dbo.CreatorProfiles', N'U') IS NOT NULL
    BEGIN
        EXEC sp_executesql N'
            IF NOT EXISTS (SELECT 1 FROM dbo.CreatorProfiles WHERE UserId = @uid)
            BEGIN
                INSERT INTO dbo.CreatorProfiles (
                    Id, UserId, DisplayName, Bio, CreatedAt, IsDeleted, IsAiVerified, CommissionSlots, CompletedOrdersCount
                ) VALUES (
                    NEWID(), @uid, N''Minh Châu Art Studio'',
                    N''Hoạ sĩ chuyên vẽ nhân vật phong cách anime, fantasy và chibi. Nhận đơn thương mại cho game, light novel, VTuber.'',
                    SYSDATETIMEOFFSET(), 0, 0, 5, 0
                );
            END',
            N'@uid UNIQUEIDENTIFIER', @uid = @ArtistUserId;
    END
END
GO

PRINT N'Database creation script with enhancements completed successfully!';
GO
