## 📋 Mô tả thay đổi

<!-- Mô tả ngắn gọn những gì PR này thực hiện -->

## 🔗 Liên quan đến Issue / Task

<!-- Đánh số issue hoặc task ID nếu có. VD: Closes #42 -->

## 📦 Module bị ảnh hưởng

<!-- Đánh dấu [x] các module liên quan -->
- [ ] Auth / Identity
- [ ] Artist Studio
- [ ] Marketplace
- [ ] Commission
- [ ] Payment / Escrow
- [ ] Chat / SignalR
- [ ] Admin
- [ ] Common / Shared (⚠️ Bắt buộc review kỹ)

## ✅ Checklist trước khi tạo PR

- [ ] `dotnet build` thành công — **0 errors, 0 new warnings**
- [ ] Đã test chạy `dotnet run` và Swagger UI hiển thị đúng
- [ ] **Không** commit `bin/`, `obj/`, `appsettings.Development.json` có secret thật
- [ ] Nếu có Migration EF Core mới → đã chạy `dotnet ef database update` thành công
- [ ] Endpoint mới có `[ProducesResponseType]` annotation cho Swagger
- [ ] Đã đọc docs module liên quan trước khi code

## 🧪 Cách test thủ công

<!-- Mô tả các bước để reviewer có thể test: VD: gọi API nào, payload mẫu... -->

```
POST https://localhost:7081/api/v1/...
Body: { ... }
Expected: 200 OK + { data: ... }
```

## 📸 Screenshots (nếu có)

<!-- Dán screenshot Swagger, Postman hoặc log nếu hữu ích -->

## 📝 Ghi chú cho Reviewer

<!-- Điều gì cần lưu ý đặc biệt, quyết định design nào cần được đồng ý -->
