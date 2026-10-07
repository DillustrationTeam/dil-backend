-- ====================================================================
-- Script Cập Nhật Vai Trò Head Jury / Jury duy nhất cho Lê Minh Châu
-- ====================================================================

DECLARE @HeadJuryCreatorId UNIQUEIDENTIFIER = 'BD231357-B012-42EB-B822-8045A42250D1';
DECLARE @DuplicateCreatorId UNIQUEIDENTIFIER = 'CD8561F7-CAD7-4D88-9314-10C4BE8ABACC';

-- 1. Xóa toàn bộ dữ liệu phân công Jury và CreatorProfile trùng lặp của account thừa
DELETE FROM CriteriaScores WHERE GradedByJuryId IN (SELECT Id FROM Juries WHERE CreatorId = @DuplicateCreatorId);
DELETE FROM Juries WHERE CreatorId = @DuplicateCreatorId;

DECLARE @DuplicateUserId UNIQUEIDENTIFIER;
SELECT @DuplicateUserId = UserId FROM CreatorProfiles WHERE Id = @DuplicateCreatorId;

-- Reassign any artworks from duplicate profile to main Head Jury profile
UPDATE Artworks SET CreatorProfileId = @HeadJuryCreatorId WHERE CreatorProfileId = @DuplicateCreatorId;

DELETE FROM CreatorProfiles WHERE Id = @DuplicateCreatorId;
IF @DuplicateUserId IS NOT NULL AND @DuplicateUserId NOT IN (SELECT UserId FROM CreatorProfiles WHERE Id = @HeadJuryCreatorId)
BEGIN
    DELETE FROM AspNetUsers WHERE Id = @DuplicateUserId;
END

-- 2. Thêm bản ghi Jury cho các Event với CreatorId duy nhất của Lê Minh Châu
IF NOT EXISTS (SELECT 1 FROM Juries WHERE EventId = '7843030D-0582-4079-A4C0-3D61DFDE47FF' AND CreatorId = @HeadJuryCreatorId)
BEGIN
    INSERT INTO Juries (Id, EventId, CreatorId, IsHeadJury, CreatedAt, IsDeleted)
    VALUES (NEWID(), '7843030D-0582-4079-A4C0-3D61DFDE47FF', @HeadJuryCreatorId, 1, SYSDATETIMEOFFSET(), 0);
END

IF NOT EXISTS (SELECT 1 FROM Juries WHERE EventId = 'E1000000-0000-0000-0000-000000000001' AND CreatorId = @HeadJuryCreatorId)
BEGIN
    INSERT INTO Juries (Id, EventId, CreatorId, IsHeadJury, CreatedAt, IsDeleted)
    VALUES (NEWID(), 'E1000000-0000-0000-0000-000000000001', @HeadJuryCreatorId, 0, SYSDATETIMEOFFSET(), 0);
END

IF NOT EXISTS (SELECT 1 FROM Juries WHERE EventId = 'E2000000-0000-0000-0000-000000000002' AND CreatorId = @HeadJuryCreatorId)
BEGIN
    INSERT INTO Juries (Id, EventId, CreatorId, IsHeadJury, CreatedAt, IsDeleted)
    VALUES (NEWID(), 'E2000000-0000-0000-0000-000000000002', @HeadJuryCreatorId, 0, SYSDATETIMEOFFSET(), 0);
END

IF NOT EXISTS (SELECT 1 FROM Juries WHERE EventId = 'E3000000-0000-0000-0000-000000000003' AND CreatorId = @HeadJuryCreatorId)
BEGIN
    INSERT INTO Juries (Id, EventId, CreatorId, IsHeadJury, CreatedAt, IsDeleted)
    VALUES (NEWID(), 'E3000000-0000-0000-0000-000000000003', @HeadJuryCreatorId, 0, SYSDATETIMEOFFSET(), 0);
END

-- 3. Cập nhật vai trò HeadJury cho event AURA 2026
UPDATE j
SET j.IsHeadJury = 0, j.UpdatedAt = SYSDATETIMEOFFSET()
FROM Juries j
WHERE j.CreatorId = @HeadJuryCreatorId
  AND j.EventId IN ('E1000000-0000-0000-0000-000000000001', 'E2000000-0000-0000-0000-000000000002', 'E3000000-0000-0000-0000-000000000003');

UPDATE j
SET j.IsHeadJury = 1, j.UpdatedAt = SYSDATETIMEOFFSET()
FROM Juries j
WHERE j.CreatorId = @HeadJuryCreatorId
  AND j.EventId = '7843030D-0582-4079-A4C0-3D61DFDE47FF';

-- 4. Kiểm tra lại kết quả
SELECT 
    j.Id AS JuryId,
    j.EventId,
    pe.Title AS EventTitle,
    j.CreatorId,
    j.IsHeadJury,
    cp.DisplayName AS CreatorDisplayName,
    u.FullName AS UserFullName
FROM Juries j
INNER JOIN PlatformEvents pe ON j.EventId = pe.Id
INNER JOIN CreatorProfiles cp ON j.CreatorId = cp.Id
LEFT JOIN AspNetUsers u ON cp.UserId = u.Id
WHERE j.CreatorId = @HeadJuryCreatorId;
