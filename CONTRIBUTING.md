# 🤝 Hướng Dẫn Đóng Góp — dil-backend (dillustration Backend)

> Đọc file này trước khi bắt đầu code lần đầu. Sau đó đọc thêm theo thứ tự trong [`AGENTS.md`](AGENTS.md).

---

## 🚀 Setup Môi Trường Phát Triển

### Yêu Cầu
| Công cụ | Phiên bản | Link |
|:---|:---|:---|
| .NET SDK | 8.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| SQL Server | 2019+ hoặc LocalDB | đi kèm Visual Studio |
| Visual Studio | 2022 (17.8+) hoặc VS Code + C# DevKit | |
| Git | 2.40+ | [git-scm.com](https://git-scm.com) |

### Các Bước Setup Lần Đầu

```bash
# 1. Clone repo
git clone https://github.com/<org>/dil-backend.git
cd dil-backend

# 2. Tạo file cấu hình thật từ mẫu (CHỈ LÀM LẦN ĐẦU)
copy src\ArtCommission.API\appsettings.Example.json src\ArtCommission.API\appsettings.Development.json
# → Sau đó mở appsettings.Development.json và điền ConnectionStrings, JWT Secret thật

# 3. Restore packages
dotnet restore

# 4. Áp dụng database migrations
dotnet ef database update -p src/ArtCommission.Infrastructure -s src/ArtCommission.API

# 5. Chạy server (port mặc định: https://localhost:7081)
dotnet run --project src/ArtCommission.API

# 6. Kiểm tra Swagger UI
# Mở trình duyệt: https://localhost:7081/swagger
```

---

## 📋 Quy Trình Làm Việc Hàng Ngày

### 1. Trước khi bắt đầu task mới:
```bash
git checkout main
git pull origin main
git checkout -b feature/<dev-id>-<module>-<mo-ta-ngan>
```

### 2. Trong lúc code:
- Đọc docs liên quan module trước khi sửa code
- Chạy `dotnet build` thường xuyên để phát hiện lỗi sớm
- Nếu sửa Entity / DB Schema → chạy migration ngay:
  ```bash
  dotnet ef migrations add <TênMigration> -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
  dotnet ef database update -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
  ```

### 3. Trước khi tạo Pull Request:
```bash
dotnet build          # ✅ Must pass: 0 errors, 0 new warnings
dotnet run            # ✅ Must start correctly
# Kiểm tra Swagger UI có hiển thị endpoint mới không
```

---

## 🌿 Quy Tắc Đặt Tên Branch

```
feature/<dev-id>-<module>-<mo-ta-ngan>   → feature/nam-commission-create-endpoint
fix/<dev-id>-<module>-<mo-ta-ngan>       → fix/nam-auth-refresh-token-rotation
chore/<dev-id>-<mo-ta-ngan>              → chore/nam-update-workflow-docs
```

Danh sách `<module>` hợp lệ: `auth`, `artist-studio`, `marketplace`, `commission`, `payment`, `chat`, `admin`

---

## 📝 Quy Tắc Commit Message (Conventional Commits)

```
feat(commission): add create milestone endpoint
fix(auth): guard refresh token against concurrent rotation
docs(workflow): add EF migration checklist
refactor(payment): extract escrow release logic to service
test(auth): add unit tests for JWT token generation
chore: bump EF Core to 8.0.11
```

**Prefix bắt buộc:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`  
**⚠️ Không thêm `Co-authored-by`** trong commit message.

---

## 🔀 Quy Trình Pull Request

1. Tạo PR từ `feature-branch` → `main`
2. Điền đầy đủ PR Template (tự động hiện khi tạo PR)
3. Cần **ít nhất 1 thành viên approve** trước khi merge
4. **Không self-merge** (không tự merge PR của mình)
5. Giải quyết tất cả review comments trước khi merge

---

## 📦 Phân Chia Module & Ownership

| Module | Thư mục Application | Thư mục Controller |
|:---|:---|:---|
| Auth & Identity | `Application/Auth/` | `Controllers/AuthController.cs` |
| Artist Studio | `Application/ArtistStudio/` | `Controllers/ArtistStudioController.cs` |
| Marketplace | `Application/Marketplace/` | `Controllers/MarketplaceController.cs` |
| Commission | `Application/Commission/` | `Controllers/CommissionController.cs` |
| Payment & Escrow | `Application/Payment/` | `Controllers/PaymentController.cs` |
| Chat & SignalR | `Application/Chat/` | `Hubs/ChatHub.cs` |
| Admin | `Application/Admin/` | `Controllers/AdminController.cs` |

> **Nguyên tắc:** Mỗi thành viên chịu trách nhiệm 1-2 module. Muốn sửa file trong `Common/Interfaces/` hoặc `Domain/` phải thông báo với cả team.

---

## ❓ Cần Giúp Đỡ?

- Đọc lại `docs/` theo thứ tự trong `AGENTS.md`
- Hỏi trong kênh Discord/Zalo của team
- Tạo Issue trên GitHub để thảo luận feature/bug
