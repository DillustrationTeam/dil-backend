-- Schema-first DDL cho BE-01/BE-09. EF migration tương ứng:
-- 20260930163701_AuctionConcurrencyAndAutoBid.
-- Chạy một lần trên SQL Server đã có Auctions, Bids và Users.

IF COL_LENGTH(N'dbo.Auctions', N'RowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Auctions ADD RowVersion rowversion NOT NULL;
END;
GO

IF OBJECT_ID(N'dbo.AuctionAutoBids', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuctionAutoBids
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_AuctionAutoBids PRIMARY KEY,
        AuctionId uniqueidentifier NOT NULL,
        BidderId uniqueidentifier NOT NULL,
        MaxAmount decimal(18,2) NOT NULL,
        RegisteredAt datetimeoffset NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_AuctionAutoBids_IsDeleted DEFAULT (0),
        CONSTRAINT FK_AuctionAutoBids_Auctions_AuctionId
            FOREIGN KEY (AuctionId) REFERENCES dbo.Auctions(Id) ON DELETE CASCADE,
        CONSTRAINT FK_AuctionAutoBids_Users_BidderId
            FOREIGN KEY (BidderId) REFERENCES dbo.Users(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AuctionAutoBids_Auction_Bidder' AND object_id = OBJECT_ID(N'dbo.AuctionAutoBids'))
    CREATE UNIQUE INDEX UX_AuctionAutoBids_Auction_Bidder ON dbo.AuctionAutoBids(AuctionId, BidderId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuctionAutoBids_ProxyOrder' AND object_id = OBJECT_ID(N'dbo.AuctionAutoBids'))
    CREATE INDEX IX_AuctionAutoBids_ProxyOrder ON dbo.AuctionAutoBids(AuctionId, MaxAmount, RegisteredAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuctionAutoBids_BidderId' AND object_id = OBJECT_ID(N'dbo.AuctionAutoBids'))
    CREATE INDEX IX_AuctionAutoBids_BidderId ON dbo.AuctionAutoBids(BidderId);
GO

;WITH RankedAutoBids AS
(
    SELECT AuctionId, BidderId, MaxAutoBid,
           MIN(PlacedAt) OVER (PARTITION BY AuctionId, BidderId) AS RegisteredAt,
           ROW_NUMBER() OVER (PARTITION BY AuctionId, BidderId ORDER BY PlacedAt DESC, Id DESC) AS LatestRank
    FROM dbo.Bids
    WHERE IsAuto = 1 AND MaxAutoBid IS NOT NULL AND IsDeleted = 0
)
INSERT INTO dbo.AuctionAutoBids
    (Id, AuctionId, BidderId, MaxAmount, RegisteredAt, CreatedAt, UpdatedAt, IsDeleted)
SELECT NEWID(), ranked.AuctionId, ranked.BidderId, ranked.MaxAutoBid,
       ranked.RegisteredAt, ranked.RegisteredAt, NULL, 0
FROM RankedAutoBids AS ranked
WHERE ranked.LatestRank = 1
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.AuctionAutoBids existing
      WHERE existing.AuctionId = ranked.AuctionId AND existing.BidderId = ranked.BidderId
  );
GO
