-- ====================================================================
-- Script Cập Nhật Database: Xóa bài dự thi và dữ liệu liên quan
-- của tác giả (Creator) nếu họ là Ban Giám Khảo (Jury) của Event đó
-- ====================================================================

-- 1. Xóa các lượt bình chọn (EventVotes) liên quan đến các bài dự thi vi phạm
DELETE v
FROM dbo.EventVotes v
INNER JOIN dbo.EventSubmissions s ON v.SubmissionId = s.Id
INNER JOIN dbo.CreatorProfiles cp ON s.SubmitterId = cp.UserId
INNER JOIN dbo.Juries j ON j.EventId = s.EventId AND j.CreatorId = cp.Id
WHERE j.IsDeleted = 0;

-- 2. Xóa các điểm số tiêu chí (CriteriaScores) liên quan
IF OBJECT_ID(N'dbo.CriteriaScores', N'U') IS NOT NULL
BEGIN
    DELETE cs
    FROM dbo.CriteriaScores cs
    INNER JOIN dbo.EventSubmissions s ON cs.SubmissionId = s.Id
    INNER JOIN dbo.CreatorProfiles cp ON s.SubmitterId = cp.UserId
    INNER JOIN dbo.Juries j ON j.EventId = s.EventId AND j.CreatorId = cp.Id
    WHERE j.IsDeleted = 0;
END

-- 3. Xóa các bài nộp dự thi (EventSubmissions) của tác giả là Jury trong event đó
DELETE s
FROM dbo.EventSubmissions s
INNER JOIN dbo.CreatorProfiles cp ON s.SubmitterId = cp.UserId
INNER JOIN dbo.Juries j ON j.EventId = s.EventId AND j.CreatorId = cp.Id
WHERE j.IsDeleted = 0;

-- 4. Cập nhật lại số lượng VoteCount chính xác cho toàn bộ EventSubmissions
UPDATE s
SET s.VoteCount = (
    SELECT COUNT(*) 
    FROM dbo.EventVotes v 
    WHERE v.SubmissionId = s.Id
)
FROM dbo.EventSubmissions s;

PRINT N'Cleaned up all invalid Jury submissions successfully!';
