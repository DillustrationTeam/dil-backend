# Hướng dẫn vẽ Class Diagram — Escrow Management

## 1. Phạm vi

Sơ đồ chỉ mô tả ký quỹ commission:

- Kiểm tra số dư ví của Client.
- Chuyển tiền từ `Balance` sang `LockedBalance` khi đặt cọc.
- Giải ngân tiền của một milestone từ ví Client sang ví Creator.
- Hoàn tiền đang giữ về số dư khả dụng khi chính sách cho phép.
- Ghi `WalletTransaction` cho từng biến động.
- Cập nhật `EscrowHeldAmount`, `DisbursedAmount` và `EscrowStatus` của commission.

Không vẽ tạo commission, WIP, watermark, review, PayOS, PaymentOrder hoặc PayoutRequest.

## 2. Bố cục Draw.io

Vẽ 5 hàng từ trên xuống, không dùng box lớn bao quanh tầng:

```text
TẦNG 1 — API / WEB
EscrowRequest DTO → EscrowController → EscrowDto

TẦNG 2 — SERVICE / APPLICATION
IEscrowService ← EscrowService

TẦNG 3 — DATA ACCESS / REPOSITORY
IWalletRepository ← WalletRepository → BaseRepository<Wallet>
ICommissionEscrowRepository ← CommissionEscrowRepository

TẦNG 4 — DAO / ORM CONTEXT
WalletDAO + CommissionEscrowDAO → BaseDAO<T> → AppDbContext

TẦNG 5 — DOMAIN / ENTITIES
BaseEntity ← Commission, Milestone, Wallet, WalletTransaction
```

> `EscrowController`, `IEscrowService` và các repository/DAO là cách tách module theo SDD mục tiêu. Code hiện tại đang đặt các endpoint escrow trong `CommissionsController` và logic trong `CommissionService`/`WalletService`.

## 3. Tầng 1 — API / Web

### DepositEscrowRequest

```text
<<dto>>
DepositEscrowRequest
------------------------------
+ CommissionId: Guid
+ PaymentMethod: string
```

### ReleaseMilestoneEscrowRequest

```text
<<dto>>
ReleaseMilestoneEscrowRequest
------------------------------
+ CommissionId: Guid
+ MilestoneId: Guid
```

### RefundEscrowRequest

```text
<<dto>>
RefundEscrowRequest
------------------------------
+ CommissionId: Guid
+ Reason: string
```

### EscrowController

```text
<<controller>>
EscrowController
------------------------------
- escrowService: IEscrowService
------------------------------
+ DepositEscrow(request)
+ ReleaseMilestoneEscrow(request)
+ RefundEscrow(request)
+ GetEscrowStatus(commissionId)
```

### EscrowDto

```text
<<dto>>
EscrowDto
------------------------------
+ CommissionId: Guid
+ FinalPrice: decimal
+ EscrowHeldAmount: decimal
+ DisbursedAmount: decimal
+ EscrowStatus: string
+ CurrentStage: int
```

## 4. Tầng 2 — Service / Application

### IEscrowService

```text
<<interface>>
IEscrowService
------------------------------
+ DepositAsync(commissionId, clientId, paymentMethod)
+ ReleaseMilestoneAsync(commissionId, milestoneId, clientId)
+ RefundAsync(commissionId, userId, reason)
+ GetStatusAsync(commissionId, userId)
```

### EscrowService

```text
<<service>>
EscrowService
------------------------------
- commissionRepository: ICommissionEscrowRepository
- walletRepository: IWalletRepository
- notificationPublisher: INotificationPublisher
- logger: ILogger<EscrowService>
------------------------------
+ DepositAsync(...)
+ ReleaseMilestoneAsync(...)
+ RefundAsync(...)
+ GetStatusAsync(...)
```

Quy tắc nghiệp vụ ghi chú cạnh `EscrowService`:

```text
Deposit:
Balance -= FinalPrice
LockedBalance += FinalPrice

Release milestone:
Client.LockedBalance -= Milestone.Price
Creator.Balance += Milestone.Price

Refund:
LockedBalance -= Amount
Balance += Amount
```

## 5. Tầng 3 — Data Access / Repository

### ICommissionEscrowRepository

```text
<<interface>>
ICommissionEscrowRepository
------------------------------
+ GetForUpdateAsync(commissionId)
+ GetMilestoneAsync(commissionId, milestoneId)
+ UpdateEscrowAsync(commission)
+ BeginTransactionAsync()
+ SaveChangesAsync()
```

### CommissionEscrowRepository

```text
<<repository>>
CommissionEscrowRepository
------------------------------
- commissionEscrowDAO: CommissionEscrowDAO
------------------------------
+ GetForUpdateAsync(commissionId)
+ GetMilestoneAsync(commissionId, milestoneId)
+ UpdateEscrowAsync(commission)
+ BeginTransactionAsync()
+ SaveChangesAsync()
```

### IWalletRepository

```text
<<interface>>
IWalletRepository
------------------------------
+ GetOrCreateByUserIdAsync(userId)
+ HoldFundsAsync(wallet, amount, commissionId)
+ ReleaseFundsAsync(clientWallet, creatorWallet, amount, milestoneId)
+ RefundHeldFundsAsync(wallet, amount, commissionId)
+ AddTransactionAsync(transaction)
+ SaveChangesAsync()
```

### WalletRepository

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
+ AddTransactionAsync(transaction)
+ SaveChangesAsync()
```

### BaseRepository&lt;T&gt;

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

## 6. Tầng 4 — DAO / ORM Context

### CommissionEscrowDAO

```text
<<dao>>
CommissionEscrowDAO
------------------------------
+ GetForUpdateAsync(commissionId)
+ GetMilestoneAsync(commissionId, milestoneId)
+ Update(commission)
```

### WalletDAO

```text
<<dao>>
WalletDAO
------------------------------
+ GetByUserIdAsync(userId)
+ AddTransactionAsync(transaction)
+ Update(wallet)
```

### BaseDAO&lt;T&gt;

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

### AppDbContext

```text
<<dbcontext>>
AppDbContext
------------------------------
+ Commissions: DbSet<Commission>
+ Milestones: DbSet<Milestone>
+ Wallets: DbSet<Wallet>
+ WalletTransactions: DbSet<WalletTransaction>
------------------------------
+ SaveChangesAsync()
+ BeginTransactionAsync()
```

Đặt ghi chú `Single database transaction` cạnh `AppDbContext` vì cập nhật commission, hai ví và các transaction phải được commit cùng nhau.

## 7. Tầng 5 — Domain / Entities

### BaseEntity

```text
<<abstract>>
BaseEntity
------------------------------
+ Id: Guid
+ CreatedAt: DateTimeOffset
+ UpdatedAt: DateTimeOffset?
+ IsDeleted: bool
```

### Commission

```text
<<entity>>
Commission
------------------------------
+ ClientId: Guid
+ CreatorId: Guid
+ FinalPrice: decimal
+ EscrowHeldAmount: decimal
+ DisbursedAmount: decimal
+ EscrowStatus: EscrowStatus
+ CurrentStage: int
+ Status: CommissionStatus
```

### Milestone

```text
<<entity>>
Milestone
------------------------------
+ CommissionId: Guid
+ Sequence: int
+ Price: decimal
+ Status: MilestoneStatus
+ ApprovedAt: DateTimeOffset?
```

### Wallet

```text
<<entity>>
Wallet
------------------------------
+ UserId: Guid
+ Balance: decimal
+ LockedBalance: decimal
+ Currency: string
+ Status: WalletStatus
+ RowVersion: byte[]?
```

### WalletTransaction

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
+ Note: string?
```

## 8. Các đường nối

| Từ | Đến | Quan hệ |
|---|---|---|
| Request DTOs | `EscrowController` | Association, `input` |
| `EscrowController` | `EscrowDto` | Association, `output` |
| `EscrowController` | `IEscrowService` | Dependency `<<uses>>` |
| `EscrowService` | `IEscrowService` | Realization `<<implements>>` |
| `EscrowService` | Hai repository interface | Association `injects`, multiplicity `1` |
| Repository implementations | Repository interfaces | Realization |
| Repository implementations | `BaseRepository<T>` | Generalization |
| Repository implementations | DAO tương ứng | Association `uses` |
| DAO | `BaseDAO<T>` | Generalization |
| `BaseDAO<T>` | `AppDbContext` | Association `uses` |
| `Commission` | `Milestone` | Composition `1` — `1..*` |
| `Wallet` | `WalletTransaction` | Composition `1` — `0..*` |
| `WalletTransaction` | `Commission` | Dependency `0..*` — `1`, nhãn `references` |
| Các entity | `BaseEntity` | Generalization |

## 9. Luồng mũi tên nghiệp vụ nên đánh số

### Đặt cọc

```text
1. EscrowController → IEscrowService
2. EscrowService → ICommissionEscrowRepository
3. EscrowService → IWalletRepository
4. WalletRepository → WalletDAO
5. CommissionEscrowRepository → CommissionEscrowDAO
6. DAO → AppDbContext → Database transaction commit
```

### Giải ngân milestone

```text
1. Đọc Commission và Milestone hiện tại
2. Giảm LockedBalance của ví Client
3. Tăng Balance của ví Creator
4. Tạo WalletTransaction cho hai ví
5. Giảm EscrowHeldAmount
6. Tăng DisbursedAmount
7. Commit toàn bộ trong một transaction
```

## 10. Màu đề xuất

- DTO: `#DAE8FC`.
- Controller: `#D5E8D4`.
- Service: `#E1D5E7`.
- Repository: `#FFF2CC`.
- DAO/DbContext: `#F8CECC`.
- Entity: `#FFE6CC`.
- Base class: `#F5F5F5`.

