# Database Schema Notes — Art Commission Marketplace

Ghi chú giải thích cho [`Database_Schema_MSSQL.sql`](Database_Schema_MSSQL.sql) (bản chạy chính thức trên SQL Server, database `ArtCommissionDB`). Bản `Database_Schema.sql` là bản draft/tham khảo tương đương, không có named constraints.

Dựa trên tài liệu `PhanCong_SRS` — 31 Use Case, 31 màn hình, 6 module: **Auth**, **Portfolio & Search**, **Commission Flow**, **Payment/Escrow**, **Admin Panel**, **Real-time Chat/Notification**.

---

## 1. Module Auth & Account — UC-001 → UC-004

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `Roles` | Danh sách role: Buyer, Artist, Admin | Seed sẵn 3 dòng cuối script |
| `Users` | Tài khoản chính | `Status`: Active / Banned / PendingVerification (CHECK constraint) |
| `PasswordResetTokens` | UC-003 Quên mật khẩu/Reset | Token có `ExpiresAt`, `UsedAt` để chống replay |
| `RefreshTokens` | JWT refresh token | Lưu `TokenHash` chứ không lưu token gốc |

**Quyết định thiết kế:** Guest không có bảng riêng — Guest = chưa đăng nhập, không có `UserId`. Role được tách bảng (`Roles`) thay vì enum cứng trong `Users` để Admin có thể mở rộng role sau này mà không cần đổi schema.

---

## 2. Module Portfolio & Search — UC-005 → UC-014

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `ArtistProfiles` | Hồ sơ họa sĩ (1-1 với `Users`) | `IsApproved` set bởi Admin (UC-025); `RatingAverage`/`FollowerCount` là cột denormalized để tránh COUNT/AVG mỗi lần load trang |
| `Follows` | UC-010 Follow/Unfollow | Unique (FollowerUserId, ArtistProfileId) — chặn follow trùng |
| `ServicePackages` | UC-013 Rate Card | Giá, deadline, số lần revision cho từng gói dịch vụ |
| `Slots` | UC-013 Slot management | 1-1 với ArtistProfile; `CHECK UsedSlots <= TotalSlots` |
| `Artworks` | UC-008/011 Upload & xem chi tiết tranh mẫu | `ModerationStatus`: Pending/Approved/Rejected; có sẵn field cho AI Detection (`IsAIGeneratedFlag`, `AIDetectionScore`) |
| `Tags` / `ArtworkTags` | Gắn tag cho tranh (search, filter) | `Tags.Source` phân biệt tag do người dùng gõ tay hay AI Auto-tagging Service sinh ra |
| `ArtworkModerationLogs` | UC-026 Kiểm duyệt tranh mẫu | `ReviewedByUserId` cho phép NULL = do AI quét tự động, khác với Admin duyệt tay |

**Quyết định thiết kế:** `RatingAverage`/`FollowerCount`/`ViewCount` là các cột đếm sẵn (denormalize) thay vì luôn tính từ bảng con — đánh đổi lấy tốc độ đọc cho trang Marketplace Home/Artist Profile (đọc nhiều hơn ghi rất nhiều lần).

---

## 3. Module Commission Flow — UC-015 → UC-020

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `Commissions` | UC-015 Tạo yêu cầu đặt vẽ | `Status` là state machine dạng string, ràng buộc bằng CHECK: `Requested → Accepted/Rejected → InProgress → UnderReview → Completed / Disputed / Cancelled` |
| `CommissionStatusHistory` | Audit trail chuyển trạng thái | Ghi lại `FromStatus → ToStatus`, ai đổi, khi nào — phục vụ UC-028 (Dispute cần lịch sử) |
| `CommissionProgress` | UC-018 Upload tiến độ theo giai đoạn | `Stage`: Sketch/Lineart/Coloring/Final (CHECK constraint) |
| `RevisionRequests` | UC-019 Yêu cầu sửa | Gắn với 1 `CommissionProgress` cụ thể, không gắn thẳng vào Commission |
| `CommissionReviews` | Đánh giá sau khi Complete (theo UC-020) | Unique 1-1 với Commission — mỗi đơn chỉ review 1 lần, Rating 1-5 |

**Quyết định thiết kế:** Tách `CommissionStatusHistory` khỏi `Commissions.Status` vì Commission chỉ cần biết trạng thái *hiện tại*, còn lịch sử đầy đủ chỉ cần khi có tranh chấp (Dispute) — tách bảng giúp bảng `Commissions` gọn, query nhanh cho các màn hình danh sách (UC-016, UC-017).

---

## 4. Module Payment / Escrow & Wallet — UC-021 → UC-024

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `Wallets` | UC-023 Số dư ví Artist | 1-1 với Users; `CHECK Balance >= 0` |
| `EscrowHolds` | UC-021 Logic giữ tiền cọc (Escrow) | 1-1 với Commission; `Status`: Held → PartiallyReleased → FullyReleased / Refunded; `CHECK AmountReleased <= AmountHeld` |
| `Transactions` | UC-022 Lịch sử giao dịch | Log phẳng mọi dòng tiền: DepositIn, EscrowRelease, Payout, Refund, FeeDeduction |
| `PayoutRequests` | UC-024 Yêu cầu rút tiền | Trạng thái Pending/Approved/Rejected/Paid, có `ProcessedByUserId` (Admin duyệt) |
| `PlatformFeeConfig` | UC-029 Quản lý phí sàn | Có `EffectiveFrom` để hỗ trợ đổi % phí theo thời gian mà không mất lịch sử phí cũ |

**Quyết định thiết kế:** `EscrowHolds` tách riêng khỏi `Transactions` vì escrow có vòng đời trạng thái riêng (giữ → giải ngân từng phần → giải ngân hết/hoàn tiền), trong khi `Transactions` chỉ là log sự kiện tiền phẳng, không cần biết "đang giữ bao nhiêu". Đây là điểm dễ nhầm nhất khi thiết kế Escrow — nhớ đừng gộp 2 bảng này lại.

---

## 5. Module Admin Panel — UC-025 → UC-029

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `Disputes` | UC-028 Giải quyết tranh chấp | Gắn với `Commissions`; cần join thêm `CommissionStatusHistory` + `EscrowHolds` để Admin có đủ ngữ cảnh xử lý |
| `UserSanctions` | UC-027 Ban/Unban User | Log dạng append-only (Ban/Unban/Warn), không sửa trực tiếp `Users.Status` mà không ghi log |
| `AuditLogs` | 5.4 Audit trail (yêu cầu phi chức năng) | Bảng generic: `Action` + `EntityType` + `EntityId` — dùng chung cho mọi hành động cần audit trong hệ thống, không riêng Admin |

**Lưu ý:** UC-025 (Duyệt hồ sơ họa sĩ) và UC-026 (Kiểm duyệt tranh mẫu) không có bảng riêng — chúng dùng lại field có sẵn trong `ArtistProfiles.IsApproved` và `ArtworkModerationLogs`, tránh tạo bảng thừa.

---

## 6. Module Real-time Chat & Notification — UC-030 → UC-031

| Bảng | Vai trò | Ghi chú |
|---|---|---|
| `Workrooms` | Phòng làm việc 1-1 với 1 Commission (màn hình #12) | Tạo khi Commission được Accept, không tạo trước |
| `ChatMessages` | UC-030 Chat real-time trong Workroom | SignalR Hub ghi message vào đây để persist (không chỉ broadcast) |
| `Notifications` | UC-031 Push Notification theo event | `RelatedEntityType` + `RelatedEntityId` là polymorphic reference (trỏ tới Commission, Dispute, Payout...) thay vì tạo FK cứng cho từng loại |

**Quyết định thiết kế:** `Notifications.RelatedEntityId` không có FK constraint (vì có thể trỏ tới nhiều loại bảng khác nhau tuỳ `RelatedEntityType`) — đây là đánh đổi có chủ đích giữa tính linh hoạt và toàn vẹn dữ liệu tuyệt đối; validate loại này nên làm ở tầng application/EF.

---

## Sơ đồ quan hệ chính (rút gọn)

```
Users ──1:1── ArtistProfiles ──1:N── ServicePackages
  │                  │                     │
  │                  ├──1:N── Artworks     │
  │                  ├──1:1── Slots        │
  │                  └──1:N── Follows      │
  │                                        │
  ├──1:N── Commissions (Buyer) ───N:1──────┘
  │              │
  │              ├──1:N── CommissionStatusHistory
  │              ├──1:N── CommissionProgress ──1:N── RevisionRequests
  │              ├──1:1── CommissionReviews
  │              ├──1:1── EscrowHolds
  │              ├──1:1── Workrooms ──1:N── ChatMessages
  │              └──1:N── Disputes
  │
  ├──1:1── Wallets ──1:N── PayoutRequests
  ├──1:N── Transactions
  ├──1:N── Notifications
  └──1:N── UserSanctions
```

---

## Những điểm cần xác nhận lại với GVHD / nhóm (chưa chốt cứng)

- `PlatformFeeConfig` hiện là bảng versioned theo thời gian (`EffectiveFrom`) — cần xác nhận công thức tính phí sàn áp dụng cho *toàn hệ thống* hay có thể khác nhau theo từng Artist/gói dịch vụ.
- Business rule "số lần Revision tối đa" hiện nằm ở `ServicePackages.RevisionLimit`, nhưng số lần đã dùng thực tế phải đếm qua `COUNT(RevisionRequests)` — chưa có cột đếm sẵn, có thể cần thêm nếu tần suất query cao (mục 5.1 Business Rules trong SRS).
- Milestone Auto-Approval Scheduler và Order Deadline & Reminder Worker (nhắc trong SRS mục 3.1.4, phụ trách Châu) là background job, chưa có bảng riêng lưu job state — nếu cần retry/idempotency thì nên bổ sung bảng `ScheduledJobs` hoặc dùng Hangfire/Quartz với storage riêng.
- `AIDetectionScore`, `IsAIGeneratedFlag` giả định AI Detection API trả về đồng bộ lúc upload; nếu API bất đồng bộ (webhook) thì cần thêm trạng thái "Scanning" giống `ModerationStatus`.
