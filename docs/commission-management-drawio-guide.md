# Hướng dẫn vẽ Class Diagram — Commission Management

## 1. Phạm vi

Sơ đồ này chỉ mô tả vòng đời commission:

- Tạo yêu cầu đặt vẽ.
- Creator chấp nhận, từ chối hoặc thương lượng.
- Nộp WIP, xem preview watermark và yêu cầu chỉnh sửa.
- Phê duyệt milestone về mặt trạng thái.
- Bàn giao file cuối, hoàn tất hoặc hủy commission.
- Mở tranh chấp và tạo review.

Không vẽ chi tiết giữ tiền, giải ngân, ví, giao dịch ví, PayOS, nạp tiền hoặc rút tiền. Việc kiểm tra commission đã được ký quỹ chỉ thể hiện bằng `EscrowStatus`.

## 2. Bố cục Draw.io

Vẽ 5 hàng từ trên xuống, không dùng box lớn bao quanh tầng:

```text
TẦNG 1 — API / WEB
Commission Request DTOs → CommissionsController → Response DTOs

TẦNG 2 — SERVICE / APPLICATION
ICommissionService ← CommissionService → Storage/Watermark/Notification

TẦNG 3 — DATA ACCESS / REPOSITORY
ICommissionRepository ← CommissionRepository → BaseRepository<Commission>

TẦNG 4 — DAO / ORM CONTEXT
CommissionDAO → BaseDAO<Commission> → AppDbContext

TẦNG 5 — DOMAIN / ENTITIES
BaseEntity ← Commission, Milestone, Dispute, Review
```

## 3. Tầng 1 — API / Web

Đặt `CommissionsController` ở giữa. Request DTO ở bên trái, response DTO ở bên phải.

### Request DTOs

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

```text
<<dto>>
RespondCommissionRequest
------------------------------
+ Action: string
+ NegotiatePrice: decimal?
+ RejectReason: string?
```

```text
<<dto>>
RequestRevisionRequest
------------------------------
+ FeedbackComment: string
+ ReferenceImages: List<string>?
```

```text
<<dto>>
CreateDisputeRequest
------------------------------
+ Reason: string
+ EvidenceUrls: List<string>?
```

### Controller

```text
<<controller>>
CommissionsController
------------------------------
- commissionService: ICommissionService
------------------------------
+ CreateCommission(request)
+ GetCommissions(status, role, page, pageSize)
+ GetCommissionById(id)
+ RespondCommission(id, request)
+ RespondToCounteroffer(id, request)
+ SubmitMilestoneWip(commissionId, milestoneId, file)
+ GetMilestoneWipPreview(commissionId, milestoneId)
+ ApproveMilestone(commissionId, milestoneId)
+ RequestMilestoneRevision(commissionId, milestoneId, request)
+ DeliverFinalWork(id, file)
+ CompleteCommission(id)
+ CancelCommission(id, request)
+ CreateDispute(id, request)
+ CreateReview(id, request)
```

### Response DTOs

```text
<<dto>>
CommissionDto
------------------------------
+ Id: Guid
+ Title: string
+ ClientId: Guid
+ CreatorId: Guid
+ TotalPrice: decimal
+ FinalPrice: decimal
+ EscrowStatus: string
+ CurrentStage: int
+ Status: string
+ DeadlineAt: DateTimeOffset?
```

```text
<<dto>>
CommissionDetailDto
------------------------------
+ Milestones: List<MilestoneDto>
+ Review: ReviewDto?
+ Dispute: DisputeDto?
```

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
+ RevisionCount: int
+ MaxRevisions: int
```

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

## 4. Tầng 2 — Service / Application

```text
<<interface>>
ICommissionService
------------------------------
+ CreateCommissionAsync(...)
+ GetCommissionsAsync(...)
+ GetCommissionByIdAsync(...)
+ RespondCommissionAsync(...)
+ RespondToCounterofferAsync(...)
+ SubmitMilestoneWipAsync(...)
+ GetMilestoneWipPreviewUrlAsync(...)
+ ApproveMilestoneAsync(...)
+ RequestMilestoneRevisionAsync(...)
+ DeliverFinalWorkAsync(...)
+ CompleteCommissionAsync(...)
+ CancelCommissionAsync(...)
+ CreateDisputeAsync(...)
+ CreateReviewAsync(...)
```

```text
<<service>>
CommissionService
------------------------------
- commissionRepository: ICommissionRepository
- watermarkService: IWatermarkService
- storageService: IStorageService
- voucherCheckService: IVoucherCheckService
- notificationPublisher: INotificationPublisher
- logger: ILogger<CommissionService>
```

Đặt các interface phụ trợ bên phải `CommissionService`:

```text
<<interface>> IWatermarkService
<<interface>> IStorageService
<<interface>> IVoucherCheckService
<<interface>> INotificationPublisher
<<logger>> ILogger<CommissionService>
```

## 5. Tầng 3 — Data Access / Repository

```text
<<interface>>
ICommissionRepository
------------------------------
+ GetByIdAsync(id)
+ GetDetailAsync(id)
+ GetByUserAsync(userId, role, status, page, pageSize)
+ AddAsync(commission)
+ ExistsDisputeAsync(commissionId)
+ SaveChangesAsync()
```

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
+ ExistsDisputeAsync(commissionId)
+ SaveChangesAsync()
```

```text
<<abstract>>
BaseRepository<Commission>
------------------------------
# dao: BaseDAO<Commission>
------------------------------
+ GetByIdAsync(id)
+ AddAsync(entity)
+ Update(entity)
+ SaveChangesAsync()
```

## 6. Tầng 4 — DAO / ORM Context

```text
<<dao>>
CommissionDAO
------------------------------
+ GetByIdAsync(id)
+ GetDetailAsync(id)
+ GetAllQuery()
+ AddAsync(commission)
+ GetDisputeAsync(commissionId)
```

```text
<<abstract>>
BaseDAO<Commission>
------------------------------
# context: AppDbContext
# entities: DbSet<Commission>
------------------------------
+ GetByIdAsync(id)
+ GetAllQuery()
+ AddAsync(entity)
+ Update(entity)
```

```text
<<dbcontext>>
AppDbContext
------------------------------
+ Commissions: DbSet<Commission>
+ Milestones: DbSet<Milestone>
+ Disputes: DbSet<Dispute>
+ Reviews: DbSet<Review>
------------------------------
+ SaveChangesAsync()
```

## 7. Tầng 5 — Domain / Entities

```text
<<abstract>>
BaseEntity
------------------------------
+ Id: Guid
+ CreatedAt: DateTimeOffset
+ UpdatedAt: DateTimeOffset?
+ IsDeleted: bool
```

```text
<<entity>>
Commission
------------------------------
+ Title: string
+ Description: string?
+ ClientId: Guid
+ CreatorId: Guid
+ TotalPrice: decimal
+ FinalPrice: decimal
+ EscrowStatus: EscrowStatus
+ CurrentStage: int
+ Status: CommissionStatus
+ DeadlineAt: DateTimeOffset?
```

```text
<<entity>>
Milestone
------------------------------
+ CommissionId: Guid
+ Sequence: int
+ Title: string
+ Price: decimal
+ Status: MilestoneStatus
+ OriginalWipUrl: string?
+ WipPreviewUrl: string?
+ FinalDeliverableUrl: string?
+ RevisionCount: int
+ MaxRevisions: int
```

```text
<<entity>>
Dispute
------------------------------
+ CommissionId: Guid
+ RaisedById: Guid
+ Reason: string
+ EvidenceUrls: string?
+ Status: string
+ Resolution: string?
+ AdminNote: string?
+ ResolvedAt: DateTimeOffset?
```

```text
<<entity>>
Review
------------------------------
+ CommissionId: Guid
+ ReviewerId: Guid
+ Rating: int
+ Comment: string?
+ ReviewerReply: string?
+ IsVisible: bool
```

## 8. Các đường nối

| Từ | Đến | Quan hệ |
|---|---|---|
| Request DTOs | `CommissionsController` | Association, `input` |
| `CommissionsController` | Response DTOs | Association, `output` |
| `CommissionsController` | `ICommissionService` | Dependency `<<uses>>` |
| `CommissionService` | `ICommissionService` | Realization `<<implements>>` |
| `CommissionService` | `ICommissionRepository` | Association `injects`, multiplicity `1` |
| `CommissionService` | Helper interfaces | Association `uses` |
| `CommissionRepository` | `ICommissionRepository` | Realization |
| `CommissionRepository` | `BaseRepository<Commission>` | Generalization |
| `CommissionRepository` | `CommissionDAO` | Association `uses` |
| `CommissionDAO` | `BaseDAO<Commission>` | Generalization |
| `BaseDAO<Commission>` | `AppDbContext` | Association `uses` |
| `Commission` | `Milestone` | Composition `1` — `1..*` |
| `Commission` | `Dispute` | Aggregation `1` — `0..1` |
| `Commission` | `Review` | Aggregation `1` — `0..1` |
| Các entity | `BaseEntity` | Generalization |

## 9. Màu đề xuất

- DTO: `#DAE8FC`.
- Controller: `#D5E8D4`.
- Service: `#E1D5E7`.
- Repository: `#FFF2CC`.
- DAO/DbContext: `#F8CECC`.
- Entity: `#FFE6CC`.
- Base class: `#F5F5F5`.

