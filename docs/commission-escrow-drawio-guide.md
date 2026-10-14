# Hướng dẫn vẽ Class Diagram — Commission & Escrow Management

> Tài liệu kết hợp này đã được thay thế bằng hai sơ đồ riêng: [Commission Management](commission-management-drawio-guide.md) và [Escrow Management](escrow-management-drawio-guide.md).

## 1. Phạm vi sơ đồ

Sơ đồ chỉ mô tả:

- Tạo và quản lý commission.
- Ký quỹ tiền từ ví cho commission.
- Nộp, yêu cầu chỉnh sửa và phê duyệt milestone.
- Giải ngân tiền theo milestone.
- Hoàn tất, hủy và tạo tranh chấp.
- Lưu WIP, file bàn giao và ảnh watermark.

Không vẽ các chức năng nạp ví qua cổng thanh toán, webhook, PaymentOrder, yêu cầu rút tiền hoặc PayoutRequest.

> Lưu ý: Repository và DAO trong hướng dẫn là thiết kế N-Layer mục tiêu của SDD. Code hiện tại đang truy cập `AppDbContext` trực tiếp và chưa có các lớp này.

## 2. Thiết lập trang Draw.io

1. Mở Draw.io và chọn **Blank Diagram**.
2. Vào **More Shapes** và bật **UML / UML 2.5**.
3. Chọn hướng trang **Landscape**.
4. Dùng bố cục dọc gồm 5 hàng, từ trên xuống dưới.
5. Không tạo container hoặc box lớn bao quanh từng tầng.
6. Chỉ đặt một nhãn chữ ở đầu mỗi hàng để ghi tên tầng.

Kích thước đề xuất:

| Thành phần | Kích thước gần đúng |
|---|---:|
| DTO nhỏ | 220 × 160 px |
| Controller | 300 × 260 px |
| Interface/Service | 300 × 240 px |
| Repository/DAO | 270 × 210 px |
| Entity | 260 × 220 px |

## 3. Màu sắc

| Tầng | Màu nền | Màu viền |
|---|---|---|
| API / Web | Xanh nhạt `#DAE8FC` | `#6C8EBF` |
| Service / Application | Tím nhạt `#E1D5E7` | `#9673A6` |
| Repository | Vàng nhạt `#FFF2CC` | `#D6B656` |
| DAO / ORM Context | Đỏ nhạt `#F8CECC` | `#B85450` |
| Domain / Entities | Cam nhạt `#FFE6CC` | `#D79B00` |
| Base class | Xám nhạt `#F5F5F5` | `#666666` |

## 4. Bố cục tổng thể

Vẽ theo thứ tự sau:

```text
TẦNG 1 — API / WEB
DTO Request → CommissionsController → DTO Response

TẦNG 2 — SERVICE / APPLICATION
ICommissionService ← CommissionService → Helper Interfaces

TẦNG 3 — DATA ACCESS / REPOSITORY
ICommissionRepository ← CommissionRepository → BaseRepository<T>
IWalletRepository     ← WalletRepository     → BaseRepository<T>

TẦNG 4 — DAO / ORM CONTEXT
CommissionDAO → BaseDAO<T> ← WalletDAO → AppDbContext

TẦNG 5 — DOMAIN / ENTITIES
BaseEntity ← Commission, Milestone, Dispute, Wallet, WalletTransaction
```

Giữ khoảng cách dọc giữa hai tầng khoảng 100–140 px để có chỗ đi dây.

## 5. Tầng 1 — API / Web

### 5.1. Vị trí

Đặt các box trên cùng theo thứ tự từ trái sang phải:

1. `CreateCommissionRequest`
2. `RequestRevisionRequest` và `CreateDisputeRequest`
3. `CommissionsController`
4. `CommissionDto`, `MilestoneDto` và `DisputeDto`

### 5.2. Nội dung class

#### CreateCommissionRequest

```text
<<dto>>
CreateCommissionRequest
------------------------------
+ CreatorId: Guid
+ PackageId: Guid
+ LicenseType: string
+ Title: string
+ Description: string?
+ DeadlineAt: DateTimeOffset?
+ VoucherCode: string?
```

#### RequestRevisionRequest

```text
<<dto>>
RequestRevisionRequest
------------------------------
+ FeedbackComment: string
+ ReferenceImages: List<string>?
```

#### CreateDisputeRequest

```text
<<dto>>
CreateDisputeRequest
------------------------------
+ Reason: string
+ EvidenceUrls: List<string>?
```

#### CommissionsController

```text
<<controller>>
CommissionsController
------------------------------
- commissionService: ICommissionService
------------------------------
+ CreateCommission(request)
+ DepositEscrow(id, paymentMethod)
+ SubmitMilestoneWip(commissionId, milestoneId, file)
+ ApproveMilestone(commissionId, milestoneId)
+ RequestMilestoneRevision(commissionId, milestoneId, request)
+ CompleteCommission(id)
+ CancelCommission(id, request)
+ CreateDispute(id, request)
```

#### CommissionDto

```text
<<dto>>
CommissionDto
------------------------------
+ Id: Guid
+ Title: string
+ TotalPrice: decimal
+ FinalPrice: decimal
+ EscrowHeldAmount: decimal
+ DisbursedAmount: decimal
+ EscrowStatus: string
+ Status: string
```

#### MilestoneDto

```text
<<dto>>
MilestoneDto
------------------------------
+ Id: Guid
+ CommissionId: Guid
+ Sequence: int
+ Title: string
+ Price: decimal
+ Status: string
+ WipPreviewUrl: string?
```

#### DisputeDto

```text
<<dto>>
DisputeDto
------------------------------
+ Id: Guid
+ CommissionId: Guid
+ RaisedById: Guid
+ Reason: string
+ Status: string
+ Resolution: string?
```

### 5.3. Quan hệ

- `CreateCommissionRequest` → `CommissionsController`: nét liền, mũi tên thường, nhãn `input`.
- `RequestRevisionRequest` → `CommissionsController`: nét liền, nhãn `input`.
- `CreateDisputeRequest` → `CommissionsController`: nét liền, nhãn `input`.
- `CommissionsController` → các response DTO: nét liền, nhãn `output`.
- `CommissionsController` ⇢ `ICommissionService`: nét đứt, mũi tên mở, nhãn `<<uses>>`.

## 6. Tầng 2 — Service / Application

### 6.1. Vị trí

Đặt `ICommissionService` bên trái, `CommissionService` ở giữa và các helper interface bên phải.

### 6.2. Nội dung class

#### ICommissionService

```text
<<interface>>
ICommissionService
------------------------------
+ CreateCommissionAsync(request, clientId)
+ DepositEscrowAsync(id, clientId, paymentMethod)
+ SubmitMilestoneWipAsync(commissionId, milestoneId, file, creatorId)
+ ApproveMilestoneAsync(commissionId, milestoneId, clientId)
+ RequestMilestoneRevisionAsync(commissionId, milestoneId, request, clientId)
+ CompleteCommissionAsync(id, clientId)
+ CancelCommissionAsync(id, request, userId)
+ CreateDisputeAsync(id, request, raisedById)
```

#### CommissionService

```text
<<service>>
CommissionService
------------------------------
- commissionRepository: ICommissionRepository
- walletRepository: IWalletRepository
- watermarkService: IWatermarkService
- storageService: IStorageService
- voucherCheckService: IVoucherCheckService
- notificationPublisher: INotificationPublisher
- logger: ILogger<CommissionService>
```

#### Helper interfaces

Tạo các box nhỏ:

```text
<<interface>> IWatermarkService
+ ApplyWatermarkAsync(stream, text)

<<interface>> IStorageService
+ UploadPublicAsync(stream, key, contentType)
+ UploadPrivateAsync(stream, key, contentType)
+ GeneratePresignedDownloadUrl(key, expiry)

<<interface>> IVoucherCheckService
+ CheckAsync(...)

<<interface>> INotificationPublisher
+ PublishAsync(...)
```

### 6.3. Quan hệ

- `CommissionService` ⇢ `ICommissionService`: nét đứt, tam giác rỗng, nhãn `<<implements>>`.
- `CommissionService` → `ICommissionRepository`: nét liền, nhãn `injects` và multiplicity `1`.
- `CommissionService` → `IWalletRepository`: nét liền, nhãn `injects` và multiplicity `1`.
- `CommissionService` → các helper interface: nét liền, nhãn `uses`.

## 7. Tầng 3 — Data Access / Repository

### 7.1. Vị trí

Đặt theo thứ tự:

```text
ICommissionRepository   CommissionRepository
IWalletRepository       WalletRepository       BaseRepository<T>
```

### 7.2. Nội dung class

#### ICommissionRepository

```text
<<interface>>
ICommissionRepository
------------------------------
+ GetByIdAsync(id)
+ GetDetailAsync(id)
+ GetByUserAsync(userId, role, status, page, pageSize)
+ AddAsync(commission)
+ SaveChangesAsync()
+ BeginTransactionAsync()
```

#### CommissionRepository

```text
<<repository>>
CommissionRepository
------------------------------
- commissionDAO: CommissionDAO
------------------------------
+ GetByIdAsync(id)
+ GetDetailAsync(id)
+ GetByUserAsync(...)
+ AddAsync(commission)
+ SaveChangesAsync()
```

#### IWalletRepository

```text
<<interface>>
IWalletRepository
------------------------------
+ GetOrCreateByUserIdAsync(userId)
+ HoldFundsAsync(wallet, amount, refId)
+ ReleaseFundsAsync(sourceWallet, targetWallet, amount, refId)
+ RefundHeldFundsAsync(wallet, amount, refId)
+ SaveChangesAsync()
```

#### WalletRepository

```text
<<repository>>
WalletRepository
------------------------------
- walletDAO: WalletDAO
------------------------------
+ GetOrCreateByUserIdAsync(userId)
+ HoldFundsAsync(...)
+ ReleaseFundsAsync(...)
+ RefundHeldFundsAsync(...)
+ SaveChangesAsync()
```

#### BaseRepository&lt;T&gt;

```text
<<abstract>>
BaseRepository<T>
------------------------------
# dao: BaseDAO<T>
------------------------------
+ GetByIdAsync(id)
+ AddAsync(entity)
+ Update(entity)
+ SaveChangesAsync()
```

### 7.3. Quan hệ

- `CommissionRepository` ⇢ `ICommissionRepository`: implements.
- `WalletRepository` ⇢ `IWalletRepository`: implements.
- `CommissionRepository` —▷ `BaseRepository<Commission>`: kế thừa.
- `WalletRepository` —▷ `BaseRepository<Wallet>`: kế thừa.
- `CommissionRepository` → `CommissionDAO`: association, nhãn `uses`.
- `WalletRepository` → `WalletDAO`: association, nhãn `uses`.

## 8. Tầng 4 — DAO / ORM Context

### 8.1. Vị trí

Đặt `CommissionDAO` và `WalletDAO` bên trái, `BaseDAO<T>` ở giữa, `AppDbContext` bên phải.

### 8.2. Nội dung class

#### CommissionDAO

```text
<<dao>>
CommissionDAO
------------------------------
+ GetByIdAsync(id)
+ GetDetailAsync(id)
+ GetAllQuery()
+ AddAsync(commission)
```

#### WalletDAO

```text
<<dao>>
WalletDAO
------------------------------
+ GetByUserIdAsync(userId)
+ GetTransactionsQuery(walletId)
+ AddTransactionAsync(transaction)
```

#### BaseDAO&lt;T&gt;

```text
<<abstract>>
BaseDAO<T>
------------------------------
# context: AppDbContext
# entities: DbSet<T>
------------------------------
+ GetByIdAsync(id)
+ GetAllQuery()
+ AddAsync(entity)
+ Update(entity)
```

#### AppDbContext

```text
<<dbcontext>>
AppDbContext
------------------------------
+ Commissions: DbSet<Commission>
+ Milestones: DbSet<Milestone>
+ Disputes: DbSet<Dispute>
+ Wallets: DbSet<Wallet>
+ WalletTransactions: DbSet<WalletTransaction>
------------------------------
+ SaveChangesAsync()
+ BeginTransactionAsync()
```

### 8.3. Quan hệ

- `CommissionDAO` —▷ `BaseDAO<Commission>`: kế thừa.
- `WalletDAO` —▷ `BaseDAO<Wallet>`: kế thừa.
- `BaseDAO<T>` → `AppDbContext`: nét liền, nhãn `uses`.

## 9. Tầng 5 — Domain / Entities

Đặt `BaseEntity` bên trái hoặc ở giữa phía dưới các entity. Đặt các entity còn lại trên cùng một hàng nếu đủ chỗ.

### 9.1. Nội dung class

#### BaseEntity

```text
<<abstract>>
BaseEntity
------------------------------
+ Id: Guid
+ CreatedAt: DateTimeOffset
+ UpdatedAt: DateTimeOffset?
+ IsDeleted: bool
```

#### Commission

```text
<<entity>>
Commission
------------------------------
+ Title: string
+ ClientId: Guid
+ CreatorId: Guid
+ TotalPrice: decimal
+ FinalPrice: decimal
+ EscrowHeldAmount: decimal
+ DisbursedAmount: decimal
+ EscrowStatus: EscrowStatus
+ CurrentStage: int
+ Status: CommissionStatus
```

#### Milestone

```text
<<entity>>
Milestone
------------------------------
+ CommissionId: Guid
+ Sequence: int
+ Title: string
+ Price: decimal
+ Status: MilestoneStatus
+ WipPreviewUrl: string?
+ FinalDeliverableUrl: string?
+ RevisionCount: int
+ MaxRevisions: int
```

#### Dispute

```text
<<entity>>
Dispute
------------------------------
+ CommissionId: Guid
+ RaisedById: Guid
+ Reason: string
+ Status: string
+ Resolution: string?
+ ClientRefundAmount: decimal?
+ ArtistPayAmount: decimal?
```

#### Wallet

```text
<<entity>>
Wallet
------------------------------
+ UserId: Guid
+ Balance: decimal
+ LockedBalance: decimal
+ Currency: string
+ Status: WalletStatus
```

#### WalletTransaction

```text
<<entity>>
WalletTransaction
------------------------------
+ WalletId: Guid
+ Type: WalletTransactionType
+ Direction: WalletTransactionDirection
+ Amount: decimal
+ BalanceAfter: decimal
+ LockedBalanceAfter: decimal
+ RefType: string?
+ RefId: Guid?
```

### 9.2. Quan hệ entity

- `Commission`, `Milestone`, `Dispute`, `Wallet`, `WalletTransaction` —▷ `BaseEntity`: generalization, nét liền, tam giác rỗng hướng về `BaseEntity`.
- `Commission "1" ◆── "1..*" Milestone`: composition, hình thoi đặc phía `Commission`.
- `Commission "1" ◇── "0..1" Dispute`: aggregation, hình thoi rỗng phía `Commission`.
- `Wallet "1" ◆── "0..*" WalletTransaction`: composition, hình thoi đặc phía `Wallet`.
- `WalletTransaction "0..*" ⇢ "1" Commission`: dependency nét đứt, nhãn `references`; liên kết logic thông qua `RefType` và `RefId`.

## 10. Quy ước đường nối

| Quan hệ | Kiểu đường Draw.io | Hướng |
|---|---|---|
| Uses | Nét đứt, mũi tên mở | Lớp sử dụng → interface |
| Implements | Nét đứt, tam giác rỗng | Implementation → interface |
| Generalization | Nét liền, tam giác rỗng | Lớp con → lớp cha |
| Dependency injection | Nét liền, mũi tên mở | Service → dependency |
| Composition | Nét liền, hình thoi đặc | Hình thoi nằm ở aggregate owner |
| Aggregation | Nét liền, hình thoi rỗng | Hình thoi nằm ở aggregate owner |

## 11. Checklist hoàn thiện

- [ ] Sơ đồ có đúng 5 hàng theo thứ tự từ API xuống Domain.
- [ ] Không có box lớn bao quanh từng tầng.
- [ ] Mọi class có stereotype phù hợp.
- [ ] Controller chỉ phụ thuộc `ICommissionService`.
- [ ] Service chỉ phụ thuộc repository interface và helper interface.
- [ ] Repository implementation implements repository interface.
- [ ] DAO kế thừa `BaseDAO<T>` và dùng `AppDbContext`.
- [ ] Entity kế thừa `BaseEntity`.
- [ ] Multiplicity của `Dispute` là `0..1`, không phải `0..*`.
- [ ] Không có PayOS, PaymentOrder hoặc PayoutRequest.
- [ ] Đường nối không đè lên nội dung class.
