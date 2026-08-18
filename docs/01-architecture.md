# Kiến trúc

Monolith, Clean Architecture, 4 project trong 1 solution, 1 deployable.

```
Domain          <- không phụ thuộc project nào
Application     <- phụ thuộc Domain
Infrastructure  <- phụ thuộc Domain + Application
API             <- phụ thuộc Application + Infrastructure
```

Chiều dependency chỉ đi vào trong (Infrastructure/API phụ thuộc Application/Domain, không ngược lại). `Domain` giữ 0 project reference — đừng thêm.

## Cấu trúc thư mục

```
ArtCommission.sln
global.json                 # pin SDK 8.0.421
src/
  ArtCommission.API/
    Controllers/
    Hubs/                    # ChatHub, NotificationHub (SignalR)
    Middleware/
    Program.cs
  ArtCommission.Application/
    Common/Interfaces/       # contract dùng chung giữa module
    Common/Behaviors/
    Auth/ ArtistStudio/ Commission/ Payment/ Chat/ Admin/
  ArtCommission.Domain/
    Common/BaseEntity.cs     # Id, CreatedAt, UpdatedAt, IsDeleted
    Entities/{Identity,ArtistStudio,Commission,Payment,Chat,Admin}/
    Enums/
  ArtCommission.Infrastructure/
    Persistence/              # AppDbContext, Configurations/, Migrations/
    Repositories/
    ExternalServices/{Cloudinary,Vnpay,Momo,OpenAi,AzureSearch}/
    BackgroundJobs/
```

## Quy tắc module boundary

Folder module (Identity/Auth, ArtistStudio, Commission, Payment, Chat, Admin) trong mỗi layer — chỉ tổ chức code, **không phải project riêng**. Module này không được gọi thẳng Repository/DbContext của module khác — phải qua interface khai báo ở `Application/Common/Interfaces`.

Lý do tách kiểu này (không tách project riêng như modular monolith thật): team nhỏ, thời gian đồ án ngắn, tách project tốn công quản lý reference mà lợi ích merge/boundary không bù nổi ở quy mô này. Xem chi tiết lý do trong lịch sử trao đổi kiến trúc (session trước) nếu cần justify lại với GVHD.

## 1 DbContext duy nhất

`AppDbContext` gộp toàn bộ entity, không tách theo module. Lý do: luồng Commission + Payment cần transaction atomic xuyên module (VD: duyệt milestone + release escrow phải cùng 1 transaction) — nhiều DbContext sẽ vỡ atomicity này.

## Background job

Dùng Hangfire (SQL Server storage, không cần infra mới) cho: escrow auto-release timeout, cleanup refresh token hết hạn. Đặt code trong `Infrastructure/BackgroundJobs/`.
