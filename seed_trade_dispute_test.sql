-- =====================================================================
-- SEED: Art Trade for testing Dispute function
-- Initiator (Requester): Ngo Le Minh Quan (1AA2346A-CE7F-4B40-AA1D-0E562AD3C6EC)
-- Receiver: Demo Artist Creator (E355C99C-7B88-4167-8125-F1DFE256096E)
-- Status: Accepted (Ready for testing "Raise Dispute" / "Khiếu nại")
-- Trade URL: /art-trades/EE550000-AAAA-BBBB-CCCC-000000000001
-- =====================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @tradeId     UNIQUEIDENTIFIER = 'EE550000-AAAA-BBBB-CCCC-000000000001';
DECLARE @initiatorId UNIQUEIDENTIFIER = '1AA2346A-CE7F-4B40-AA1D-0E562AD3C6EC'; -- Ngo Le Minh Quan
DECLARE @receiverId  UNIQUEIDENTIFIER = 'E355C99C-7B88-4167-8125-F1DFE256096E'; -- Demo Artist Creator
DECLARE @now         DATETIMEOFFSET   = SYSDATETIMEOFFSET();

-- Resolve actual User IDs if present in DB
SELECT TOP 1 @initiatorId = Id FROM Users WHERE Email = 'minhquaningenious@gmail.com' OR FullName LIKE N'%Ngo Le Minh Quan%';
SELECT TOP 1 @receiverId  = Id FROM Users WHERE Email = 'creator.demo@dillustration.local' OR FullName LIKE N'%Demo Artist%';

-- 1. Xoá khiếu nại (TradeDisputes) cũ liên quan
DELETE FROM TradeDisputes 
WHERE ArtTradeId IN (SELECT Id FROM ArtTrades WHERE Title LIKE N'%Cyberpunk%') 
   OR ArtTradeId = @tradeId;

-- 2. Xoá đóng băng slot (TradeSlotFreezes) cũ liên quan
DELETE FROM TradeSlotFreezes 
WHERE ArtTradeId IN (SELECT Id FROM ArtTrades WHERE Title LIKE N'%Cyberpunk%') 
   OR ArtTradeId = @tradeId;

-- 3. Xoá thông báo cũ nếu có
IF OBJECT_ID(N'Notification') IS NOT NULL
BEGIN
    DELETE FROM Notification 
    WHERE RefId = CAST(@tradeId AS NVARCHAR(50)) 
       OR Body LIKE N'%Cyberpunk%' 
       OR NotificationTitle LIKE N'%Cyberpunk%';
END

-- 4. Xoá Trade cũ
DELETE FROM ArtTrades 
WHERE Title LIKE N'%Cyberpunk%' 
   OR Id = @tradeId;

-- 5. Tạo mới giao dịch ở trạng thái Accepted
INSERT INTO ArtTrades (
    Id,
    InitiatorId,
    ReceiverId,
    Title,
    Description,
    InitiatorRequestNote,
    ReceiverRequestNote,
    ReceiverResponseNote,
    ReceiverAcceptedAt,
    Status,
    SubmissionDeadlineAt,
    ConfirmationDeadlineAt,
    CompletedAt,
    CancelledAt,
    CancelledById,
    CancellationReason,
    RejectionReason,
    IsInitiatorConfirmed,
    InitiatorConfirmedAt,
    IsReceiverConfirmed,
    ReceiverConfirmedAt,
    InitiatorSubmittedAt,
    ReceiverSubmittedAt,
    InitiatorArtworkId,
    ReceiverArtworkId,
    InitiatorOriginalFileUrl,
    InitiatorPreviewUrl,
    ReceiverOriginalFileUrl,
    ReceiverPreviewUrl,
    CreatedAt,
    UpdatedAt,
    IsDeleted
)
VALUES (
    @tradeId,
    @initiatorId,
    @receiverId,
    N'Cyberpunk OC vs Fantasy Mage Trade (Test Dispute)',
    N'Giao dịch Art Trade mẫu giữa Ngo Le Minh Quan và Demo Artist phục vụ kiểm thử tính năng Khiếu nại (Trade Dispute Resolution).',
    N'{"text":"Yêu cầu vẽ OC nữ kiếm sĩ cyberpunk tóc bạch kim, trang phục neon dạ quang và cầm trường kiếm phát sáng.","images":["https://images.unsplash.com/photo-1578632767115-351597cf2477?q=80&w=1280&auto=format&fit=crop"]}',
    N'{"text":"Yêu cầu vẽ pháp sư nguyên tố lửa phong cách anime cổ điển cùng linh thú triệu hồi.","images":["https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?q=80&w=1280&auto=format&fit=crop"]}',
    N'{"text":"Đã thống nhất các điều khoản và phong cách. Tôi bắt đầu vẽ và hẹn bàn giao đúng hạn!","images":["https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?q=80&w=1280&auto=format&fit=crop"]}',
    DATEADD(hour, -2, @now),
    'Accepted',
    DATEADD(day, 14, @now),
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    0,
    NULL,
    0,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    DATEADD(day, -1, @now),
    DATEADD(hour, -2, @now),
    0
);

-- Kiểm tra lại sau khi chèn
SELECT 
    t.Id, 
    t.Title, 
    u1.FullName AS InitiatorName, 
    u1.Email AS InitiatorEmail,
    u2.FullName AS ReceiverName, 
    u2.Email AS ReceiverEmail,
    t.Status, 
    t.CreatedAt
FROM ArtTrades t
JOIN Users u1 ON t.InitiatorId = u1.Id
JOIN Users u2 ON t.ReceiverId = u2.Id
WHERE t.Id = @tradeId;
