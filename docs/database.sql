-- ============================================================================
-- DATABASE SCHEMA CREATION SCRIPT — ArtCommission (Dillustration)
-- Target DBMS: Microsoft SQL Server 2019+ / Azure SQL Database
-- Total Tables: 35 Tables (22 Core Entities + 13 Feature Entities)
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

-- Bảng Tranh chấp (Dispute)
IF OBJECT_ID(N'dbo.Disputes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Disputes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Disputes PRIMARY KEY DEFAULT NEWID(),
        CommissionId UNIQUEIDENTIFIER NOT NULL,
        RaisedById UNIQUEIDENTIFIER NOT NULL,
        Reason NVARCHAR(2000) NOT NULL,
        EvidenceUrls NVARCHAR(MAX) NULL,      -- ⚡ [CẢI TIẾN] JSON array ảnh bằng chứng tranh chấp
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, UnderReview, Resolved
        Resolution NVARCHAR(50) NULL,          -- ⚡ [CẢI TIẾN] ClientWin100, ArtistWin100, SplitCustom
        ClientRefundAmount DECIMAL(18, 2) NULL,-- ⚡ [CẢI TIẾN] Số tiền hoàn cho Client theo phán quyết
        ArtistPayAmount DECIMAL(18, 2) NULL,   -- ⚡ [CẢI TIẾN] Số tiền trả cho Họa sĩ theo phán quyết
        AdminNote NVARCHAR(MAX) NULL,
        ResolvedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_Disputes_Commissions FOREIGN KEY (CommissionId) REFERENCES dbo.Commissions(Id),
        CONSTRAINT FK_Disputes_RaisedBy FOREIGN KEY (RaisedById) REFERENCES dbo.AspNetUsers(Id)
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
-- 8. ART TRADE MODULE (🚀 CẢI TIẾN)
-- ============================================================================

-- Bảng Bài đăng Trao đổi Tranh
IF OBJECT_ID(N'dbo.TradePosts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradePosts (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TradePosts PRIMARY KEY DEFAULT NEWID(),
        PosterId UNIQUEIDENTIFIER NOT NULL,
        OfferedArtworkId UNIQUEIDENTIFIER NOT NULL,
        WantDescription NVARCHAR(1000) NOT NULL,
        AcceptedOfferId UNIQUEIDENTIFIER NULL,        -- ⚡ [CẢI TIẾN] FK Lời đề nghị được chọn đồng ý
        OfferCount INT NOT NULL DEFAULT 0,             -- ⚡ [CẢI TIẾN] Đếm tổng số offer nhận được
        Status NVARCHAR(50) NOT NULL DEFAULT 'Open', -- Open, PendingApproval, Completed, Cancelled
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_TradePosts_Poster FOREIGN KEY (PosterId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_TradePosts_OfferedArtwork FOREIGN KEY (OfferedArtworkId) REFERENCES dbo.Artworks(Id)
    );

    CREATE INDEX IX_TradePosts_Status_CreatedAt ON dbo.TradePosts(Status, CreatedAt DESC);
END
GO

-- Bảng Lời đề nghị Trao đổi (Offer)
IF OBJECT_ID(N'dbo.TradeOffers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradeOffers (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TradeOffers PRIMARY KEY DEFAULT NEWID(),
        TradePostId UNIQUEIDENTIFIER NOT NULL,
        OffererId UNIQUEIDENTIFIER NOT NULL,
        OfferedArtworkId UNIQUEIDENTIFIER NOT NULL,
        Message NVARCHAR(500) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Accepted, Rejected
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_TradeOffers_TradePost FOREIGN KEY (TradePostId) REFERENCES dbo.TradePosts(Id) ON DELETE CASCADE,
        CONSTRAINT FK_TradeOffers_Offerer FOREIGN KEY (OffererId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_TradeOffers_OfferedArtwork FOREIGN KEY (OfferedArtworkId) REFERENCES dbo.Artworks(Id)
    );
END
GO

-- FK vòng cho AcceptedOfferId trong TradePosts
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TradePosts_AcceptedOffer')
BEGIN
    ALTER TABLE dbo.TradePosts 
    ADD CONSTRAINT FK_TradePosts_AcceptedOffer 
    FOREIGN KEY (AcceptedOfferId) REFERENCES dbo.TradeOffers(Id);
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
        Status NVARCHAR(50) NOT NULL DEFAULT 'Draft', -- Draft, Open, Judging, Ended
        StartAt DATETIMEOFFSET NOT NULL,
        EndsAt DATETIMEOFFSET NOT NULL,
        CreatedByAdminId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_PlatformEvents_Admin FOREIGN KEY (CreatedByAdminId) REFERENCES dbo.AspNetUsers(Id)
    );
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

PRINT N'Database creation script with enhancements completed successfully!';
GO
