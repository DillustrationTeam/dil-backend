# AGENTS.md — ArtCommission / Dillustration Backend

Điểm vào đầu tiên cho mọi AI agent (Claude, Copilot, Cursor...) trước khi đụng vào repo này. Đọc file này + toàn bộ `docs/` trước khi code, mỗi lần bắt đầu task mới — đảm bảo đồng bộ giữa các thành viên dù dùng AI khác nhau.

## Đọc theo thứ tự

1. [`docs/00-overview.md`](docs/00-overview.md) — dự án là gì, giới hạn scope, mốc thời gian
2. [`docs/01-architecture.md`](docs/01-architecture.md) — kiến trúc Clean Architecture, cấu trúc thư mục, module boundary
3. [`docs/02-workflow.md`](docs/02-workflow.md) — checklist trước khi code, branch/commit convention, PR, review
4. [`docs/03-database.md`](docs/03-database.md) — schema, index đã chốt, concurrency, migration command
5. [`docs/04-api-conventions.md`](docs/04-api-conventions.md) — REST pattern, response envelope, auth, SignalR, rate limit
6. [`docs/05-use-cases.md`](docs/05-use-cases.md) — danh sách 7 Actor chuẩn hóa, mô hình Multi-role User, specs 34 use cases

## Tech stack (tóm tắt — chi tiết xem docs/)

Backend: ASP.NET Core 8, EF Core 8 + SQL Server, ASP.NET Identity, JWT (15m/7d), SignalR, FluentValidation, AutoMapper, Serilog, Swashbuckle.
External: Cloudinary, VNPAY, MoMo, OpenAI API (text-only), Azure AI Search.
Frontend (repo riêng): Next.js 14, TypeScript strict, Tailwind, Axios, Zustand.
Deploy: Azure App Service, GitHub Actions, Docker.

## Quy tắc cứng — không tự ý đổi hướng khi chưa hỏi

- Kiến trúc: monolith 4-project Clean Architecture, **không** tách microservice/modular-monolith-project-riêng trừ khi người yêu cầu rõ.
- 1 `AppDbContext` duy nhất, không tách theo module.
- Index/scale strategy đã chốt trong `docs/03-database.md` — không thêm sharding/partitioning/read-replica tuỳ tiện.
- Ngoài phạm vi (đừng build): mua tranh trực tiếp, dispute resolution tự động, app di động native.
- Quyết định mới phát sinh lúc làm việc → cập nhật lại file `docs/` tương ứng ngay, đừng để trôi trong chat.

## Build / Run

```
dotnet build
dotnet run --project src/ArtCommission.API
dotnet ef migrations add <Name> -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
dotnet ef database update -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
```

## Nguồn tài liệu gốc (ngoài repo này)

Requirement/design đầy đủ (SRS, mapping màn hình, Software Design Document): repo `D:\SEP490_Dillustration`, thư mục `02_Plan_Requirement/` và `03_Software_Design/`. Không chắc feature có trong scope — kiểm tra ở đó trước.
