# Cách làm việc (Git, commit, PR, checklist)

## Trước khi bắt đầu 1 task — checklist bắt buộc

1. Đọc [`AGENTS.md`](../AGENTS.md) (hoặc `CLAUDE.md`) ở root — luôn là điểm vào đầu tiên.
2. Đọc file docs liên quan module đang sửa (VD: sửa Commission → đọc `03-database.md` phần Commission, `05-use-cases.md`).
3. Pull nhánh chính mới nhất trước khi tạo nhánh mới — tránh conflict migration EF Core (2 người cùng thêm migration trên nhánh cũ → lệch model snapshot).
4. Nếu đổi schema (thêm/sửa entity) → chạy `dotnet ef migrations add` **trước khi push**, không để người khác tự chạy giúp — tên migration phải mô tả đúng thay đổi.

## Branch naming (Bắt buộc kèm Mã định danh/Tên người làm — VD: de190123)

```
feature/<dev-id>-<module>-<mo-ta-ngan>     # feature/de190123-auth-identity-setup
fix/<dev-id>-<module>-<mo-ta-ngan>         # fix/de190123-payment-webhook-double-process
chore/<dev-id>-<mo-ta-ngan>                # chore/de190123-update-docs-auth
```

`<dev-id>` = Mã định danh/Tên thành viên thực hiện (VD: `de190123`).
`<module>` = auth, artist-studio, marketplace, commission, payment, chat, admin.

## Commit convention (Conventional Commits)

```
feat(commission): add milestone approval endpoint
fix(payment): guard escrow release against webhook retry
docs: update AGENTS.md with indexing notes
refactor(chat): extract message pagination to service
test(commission): cover milestone state machine edge cases
chore: bump EF Core to 8.0.x
```

Prefix bắt buộc: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Scope trong ngoặc = tên module.

⚠️ **Lưu ý quan trọng khi commit:** Tuyệt đối **KHÔNG đính kèm dòng `Co-authored-by`** trong commit message (chỉ giữ thông tin tác giả duy nhất là tài khoản Git của người dùng).


## Trước khi mở PR

- [ ] `dotnet build` sạch, 0 warning mới phát sinh
- [ ] Migration (nếu có) đã test `dotnet ef database update` chạy được từ đầu
- [ ] Không commit `bin/`, `obj/`, `appsettings.*.json` chứa secret thật (dùng `appsettings.Development.json` mẫu, secret thật để User Secrets / env var)
- [ ] Endpoint mới có Swagger annotation (XML doc hoặc `[ProducesResponseType]`) — Swashbuckle tự sinh doc, đừng bỏ trống

## Review

Ai sửa module nào, người review ưu tiên là người hiểu module đó (hoặc người viết `Application/Common/Interfaces` liên quan nếu đụng cross-module contract). Đụng vào `Common/` (Interfaces, Behaviors, BaseEntity) — bắt buộc có review, ảnh hưởng toàn bộ layer trên.

## Đồng bộ AI agent giữa các thành viên

Mọi AI agent (Claude Code, Copilot, Cursor...) làm việc trên repo này phải đọc `AGENTS.md`/`CLAUDE.md` + toàn bộ `docs/` trước khi code — đảm bảo dù thành viên nào dùng AI nào, quyết định kiến trúc/convention ra cùng 1 hướng. Nếu AI đề xuất lệch những gì ghi trong `docs/`, dừng lại hỏi người, đừng tự ý đổi hướng đã chốt.

Khi 1 quyết định kiến trúc/convention **mới** được chốt trong lúc làm việc với AI (không có sẵn trong docs) — cập nhật ngay vào file docs tương ứng cùng lúc, đừng để trôi mất trong lịch sử chat.
