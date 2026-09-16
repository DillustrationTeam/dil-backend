# Thiết kế Database — ArtCommission (Dillustration)

SQL Server, EF Core 8 **Database-First**, 1 `AppDbContext` duy nhất (lý do xem [`docs/01-architecture.md`](01-architecture.md)).
Tạo và quản lý Schema bảng trước trên SQL Server qua SQL DDL Scripts, sau đó ánh xạ (Mapping) vào EF Core 8 Entity Models.

---


## 1. Chuẩn hóa Entity & Base Class

Mọi Entity chính trong hệ thống kế thừa `BaseEntity` (trừ bảng liên kết n-n hoặc log append-only):
- `Id` (`Guid`): Primary Key (UUID v4).
- `CreatedAt` (`DateTimeOffset`): Thời điểm tạo record (UTC).
- `UpdatedAt` (`DateTimeOffset?`): Thời điểm cập nhật cuối (UTC).
- `IsDeleted` (`bool`): Flag hỗ trợ Soft Delete (mặc định `false`).

---

## 2. Chi tiết Auth / Identity Module (Dựa trên `Dillustration_ERD.drawio` & ASP.NET Core Identity)

Phần Auth / Identity quản lý thông tin xác thực, phân quyền 7 Actor và hỗ trợ **Mô hình Multi-role User** (1 người dùng có thể đồng thời đóng vai trò Client và Artist/Creator).

### 2.1 Bảng `AspNetUsers` (Extends `IdentityUser<Guid>`)
Đại diện cho tài khoản người dùng đã đăng ký trong hệ thống.

| Field | Type | Constraint / Option | Description |
|---|---|---|---|
| `Id` | `Guid` | PK | Mã định danh duy nhất của người dùng |
| `Email` | `nvarchar(256)` | Unique, Not Null | Địa chỉ email đăng ký |
| `NormalizedEmail` | `nvarchar(256)` | Unique Index | Email chuẩn hóa chữ hoa (chống trùng lặp) |
| `UserName` | `nvarchar(256)` | Unique, Not Null | Tên tài khoản / Email đăng nhập |
| `NormalizedUserName` | `nvarchar(256)` | Unique Index | Username chuẩn hóa chữ hoa |
| `PasswordHash` | `nvarchar(max)` | Not Null | Mật khẩu đã hash mã hóa (ASP.NET Identity PBKDF2/IdentityV3) |
| `FullName` | `nvarchar(150)` | Not Null | Họ và tên hiển thị người dùng |
| `IsVerified` | `bool` | Default `false` | Trạng thái xác thực tài khoản/email (Use Case 01) |
| `CreatedAt` | `DateTimeOffset` | Not Null | Ngày giờ tạo tài khoản |
| `UpdatedAt` | `DateTimeOffset?` | Nullable | Ngày giờ cập nhật thông tin |
| `IsDeleted` | `bool` | Default `false` | Cờ soft delete (khóa tài khoản / xóa mềm) |

*Mô hình Phân quyền (Roles):*
- Hệ thống hỗ trợ 7 Actor: `Administrator`, `Moderator`, `Creator`, `Client`, `Guest`, `Payment Gateway Service`, `AI Assistant Service`.
- Tích hợp ASP.NET Core Identity Roles (`AspNetUserRoles`, `AspNetRoles`) để gán đa vai trò cho `AspNetUsers` (VD: 1 User vừa có role `Client`, vừa có role `Creator`).

---

### 2.2 Bảng `RefreshTokens`
Quản lý chuỗi Token làm mới phiên làm việc (JWT Auth & Refresh Token Rotation).

| Field | Type | Constraint / Option | Description |
|---|---|---|---|
| `Id` | `Guid` | PK | Mã định danh token |
| `UserId` | `Guid` | FK -> `AspNetUsers.Id` | Mã người dùng sở hữu token |
| `TokenHash` | `nvarchar(450)` | Unique Index, Not Null | Chuỗi hash SHA256 của Refresh Token (không lưu plaintext) |
| `ExpiresAt` | `DateTimeOffset` | Not Null | Thời điểm token hết hạn (mặc định 7 ngày) |
| `RevokedAt` | `DateTimeOffset?` | Nullable | Thời điểm token bị hủy (logout / nghi ngờ reuse) |
| `CreatedByIp` | `nvarchar(50)` | Nullable | Địa chỉ IP gửi yêu cầu tạo token |
| `ReplacedByTokenHash` | `nvarchar(450)` | Nullable | Token hash mới thay thế khi thực hiện Rotation |
| `CreatedAt` | `DateTimeOffset` | Not Null | Ngày giờ phát hành token |

---

## 3. Bảng dữ liệu cốt lõi theo Module (Đối chiếu `Dillustration_ERD.drawio`)

### 3.1 Module ArtistStudio (Portfolio & Hồ sơ Họa sĩ)
- **`CreatorProfile`**: `Id` (PK), `UserId` (FK -> `AspNetUsers`, 1-1 Unique), `DisplayName` (`nvarchar(100)`), `Bio` (`nvarchar(1000)`), `RateCard` (`nvarchar(max)` / JSON), `CommissionSlots` (`int`), `RatingAvg` (`decimal(3,2)`), `IsAiVerified` (`bool`).
- **`Artwork`**: `Id` (PK), `CreatorId` (FK -> `CreatorProfile`), `Title` (`nvarchar(200)`), `Description` (`nvarchar(2000)`), `ImageUrl` (`nvarchar(500)`), `Style` (`nvarchar(100)`), `Status` (`enum`: Draft, Published, Hidden, Flagged).
- **`Tag`**: `Id` (PK), `Name` (`nvarchar(100)`, Unique Index), `IsAiGenerated` (`bool`).
- **`ArtworkTag`**: Composite PK (`ArtworkId`, `TagId`), `CreatedAt`.
- **`Follow`**: Composite PK (`FollowerId` -> `AspNetUsers`, `FollowingId` -> `CreatorProfile`), `CreatedAt`.

### 3.2 Module Commission (Đặt vẽ & Cột mốc Workroom)
- **`Commission`**: `Id` (PK), `Title` (`nvarchar(200)`), `Description` (`nvarchar(max)`), `ClientId` (FK -> `AspNetUsers`), `CreatorId` (FK -> `CreatorProfile`), `TotalPrice` (`decimal(18,2)`), `EscrowStatus` (`enum`: Pending, Deposited, Released, Refunded, Disputed), `CurrentStage` (`int`), `Status` (`enum`: PendingAcceptance, InProgress, Completed, Cancelled, Disputed), `CreatedAt`.
- **`Milestone`**: `Id` (PK), `CommissionId` (FK -> `Commission`), `Sequence` (`int`), `Title` (`nvarchar(200)`), `Price` (`decimal(18,2)`), `Status` (`enum`: Pending, InProgress, Submitted, Approved, RevisionRequested), `WipPreviewUrl` (`nvarchar(500)`).
- **`Review`**: `Id` (PK), `CommissionId` (FK -> `Commission`), `ReviewerId` (FK -> `AspNetUsers`), `Rating` (`int`: 1-5), `Comment` (`nvarchar(1000)`), `CreatedAt`.
- **`Dispute`**: `Id` (PK), `CommissionId` (FK -> `Commission`), `RaisedBy` (FK -> `AspNetUsers`), `Reason` (`nvarchar(2000)`), `Status` (`enum`: Pending, UnderReview, ResolvedClientWin, ResolvedArtistWin, ResolvedSplit), `AdminNote` (`nvarchar(max)`), `ResolvedAt` (`DateTimeOffset?`).

### 3.3 Module Payment & Wallet (Ví & Giao dịch Escrow)
- **`Payment`**: `Id` (PK), `CommissionId` (FK -> `Commission`), `Amount` (`decimal(18,2)`), `EscrowStatus` (`enum`), `TransactionRef` (`nvarchar(100)`), `PaidAt` (`DateTimeOffset?`).
- **`Wallet`**: `Id` (PK), `UserId` (FK -> `AspNetUsers`, 1-1 Unique), `Balance` (`decimal(18,2)`), `RowVersion` (`timestamp`), `UpdatedAt` (`DateTimeOffset`).
- **`Transaction`**: `Id` (PK), `WalletId` (FK -> `Wallet`), `CommissionId` (FK -> `Commission`, Nullable), `Type` (`enum`: Deposit, EscrowHold, EscrowRelease, Refund, Payout, PlatformFee), `Amount` (`decimal(18,2)`), `Ref` (`nvarchar(100)`), `CreatedAt`.
- **`PayoutRequest`**: `Id` (PK), `WalletId` (FK -> `Wallet`), `Amount` (`decimal(18,2)`), `Status` (`enum`: Pending, Approved, Rejected, Completed), `BankInfo` (`nvarchar(max)` / JSON), `RequestedAt` (`DateTimeOffset`), `ProcessedAt` (`DateTimeOffset?`).

### 3.4 Module Chat & Notification
- **`Message`**: `Id` (PK), `CommissionId` (FK -> `Commission`), `SenderId` (FK -> `AspNetUsers`), `Body` (`nvarchar(max)`), `SentAt` (`DateTimeOffset`), `IsRead` (`bool`).
- **`Notification`**: `Id` (PK), `UserId` (FK -> `AspNetUsers`), `Type` (`enum`), `Message` (`nvarchar(1000)`), `IsRead` (`bool`), `CreatedAt`.

---

## 4. Index đã chốt — Đảm bảo hiệu năng Query

| Bảng | Index | Lý do |
|---|---|---|
| `AspNetUsers` | `(NormalizedEmail)`, `(NormalizedUserName)` Unique | Lookup tài khoản Auth O(1) |
| `RefreshTokens` | `(TokenHash)` Unique | Validation & Rotation Token nhanh |
| `Artworks` | `(CreatorId, CreatedAt DESC)` composite | Portfolio feed sort theo thời gian |
| `Commissions` | `(Status, UpdatedAt)` filtered (`WHERE Status IN ('Active','Pending')`) | Dashboard chỉ query đơn đang sống |
| `Messages` | `(CommissionId, SentAt DESC)` composite | Phân trang lịch sử chat Workroom |
| `Wallets` | `(UserId)` Unique | Truy vấn ví người dùng |

*Lưu ý Search:* Không tạo SQL full-text index trên mô tả Artwork — search & semantic matching được đảm nhiệm bởi **Azure AI Search** (dual-write lúc artwork create/update).

---

## 5. Concurrency & Idempotency Rules

- `Wallet`, `Payment` và `Transaction`: Thiết lập **Append-only ledger** cho nhật ký giao dịch, có `RowVersion` (concurrency token) để guard race-condition khi nạp/rút tiền.
- Cổng thanh toán VNPAY/MoMo IPN Webhook: Bắt buộc dùng `TransactionRef` / idempotency key để chống xử lý 2 lần (double-credit / double-release).

---

## 6. Migration Commands (EF Core 8)

```bash
dotnet ef migrations add <Name> -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
dotnet ef database update -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
```
