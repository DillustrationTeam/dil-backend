-- ===================================================
-- SEED: Demo Commission Workroom
-- Creator: creator.demo@dillustration.test
-- Client:  client.demo@dillustration.test
-- Workroom URL: /workroom/AAAAAAAA-0000-0000-0000-000000000001
-- Password for both accounts: Demo@123456
-- ===================================================

DECLARE @commissionId UNIQUEIDENTIFIER = 'AAAAAAAA-0000-0000-0000-000000000001';
DECLARE @clientId     UNIQUEIDENTIFIER = '347DF18A-D6C1-4421-9F48-FB87D6233B35';
DECLARE @creatorId    UNIQUEIDENTIFIER = 'F60E878E-D03B-4B6E-A5A7-D9E5EE5B7E72';
DECLARE @now          DATETIMEOFFSET   = SYSDATETIMEOFFSET();

-- Ensure client Wallet exists (RowVersion is auto timestamp, exclude it)
IF NOT EXISTS (SELECT 1 FROM Wallet WHERE UserId = @clientId)
    INSERT INTO Wallet (Id, UserId, Balance, LockedBalance, Currency, Status, CreatedAt, UpdatedAt, IsDeleted)
    VALUES (NEWID(), @clientId, 0, 300000, 'VND', 'Active', @now, @now, 0);

-- Ensure creator Wallet exists  
IF NOT EXISTS (SELECT 1 FROM Wallet WHERE UserId = @creatorId)
    INSERT INTO Wallet (Id, UserId, Balance, LockedBalance, Currency, Status, CreatedAt, UpdatedAt, IsDeleted)
    VALUES (NEWID(), @creatorId, 0, 0, 'VND', 'Active', @now, @now, 0);

-- Remove old seed if exists
DELETE FROM Milestones WHERE CommissionId = @commissionId;
DELETE FROM Commissions WHERE Id = @commissionId;

-- Insert Commission (InProgress, Escrow Deposited)
INSERT INTO Commissions (
    Id, Title, Description,
    ClientId, CreatorId,
    VoucherId, DiscountAmount, TotalPrice, FinalPrice,
    EscrowHeldAmount, DisbursedAmount,
    EscrowStatus, CurrentStage, Status,
    DeadlineAt, CreatedAt, UpdatedAt, IsDeleted
)
VALUES (
    @commissionId,
    N'Demo Commission – Character Illustration',
    N'Commission minh họa nhân vật fantasy phong cách anime. Bao gồm 3 milestone: Sketch, Line Art, và Final Color.',
    @clientId,
    @creatorId,
    NULL, 0, 300000, 300000,
    300000, 0,
    'Deposited', 1, 'InProgress',
    DATEADD(day, 30, @now), @now, @now, 0
);

-- Insert 3 Milestones
INSERT INTO Milestones (
    Id, CommissionId, Sequence, Title, Price, Status,
    WipPreviewUrl, WatermarkedUrl, FinalDeliverableUrl,
    RevisionCount, SubmittedAt, ApprovedAt,
    CreatedAt, UpdatedAt, IsDeleted,
    FinalObjectKey, WipObjectKey, RevisionFeedback
)
VALUES
(NEWID(), @commissionId, 1, N'Bản phác thảo (Sketch)', 100000, 'Pending',
 NULL, NULL, NULL, 0, NULL, NULL, @now, @now, 0, NULL, NULL, NULL),
(NEWID(), @commissionId, 2, N'Tô nét (Line Art)', 100000, 'Pending',
 NULL, NULL, NULL, 0, NULL, NULL, @now, @now, 0, NULL, NULL, NULL),
(NEWID(), @commissionId, 3, N'Tô màu hoàn chỉnh (Final Color)', 100000, 'Pending',
 NULL, NULL, NULL, 0, NULL, NULL, @now, @now, 0, NULL, NULL, NULL);

SELECT 'Seed done!' AS Result;
SELECT 'http://localhost:3000/workroom/AAAAAAAA-0000-0000-0000-000000000001' AS WorkroomURL;
