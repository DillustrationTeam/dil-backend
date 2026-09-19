# API convention

REST, không GraphQL/tRPC (lý do: backend C# + frontend TS khác stack, tRPC cần chung type; GraphQL thừa phức tạp cho use case hiện tại).

## Versioning

URI-based: `/api/v1/...`. Không dùng header/query versioning.

## Response envelope

```json
{ "data": { }, "meta": { "page": 1, "totalPages": 10 }, "error": null }
```

Mọi endpoint trả về đúng shape này, kể cả list rỗng hay lỗi. Không trả raw array ở top level.

## Error

Mọi phản hồi REST do ứng dụng tạo dùng envelope. Với lỗi, `data` và `meta` là `null`, còn `error` chứa các trường của ASP.NET Core `ProblemDetails` (`type`, `title`, `status`, `detail`) và `traceId`; lỗi validation có thêm `errors` theo tên trường. Mã HTTP khớp với `error.status`. Ví dụ:

```json
{ "data": null, "meta": null, "error": { "type": "https://httpstatuses.com/404", "title": "Not Found", "status": 404, "detail": "Resource not found.", "traceId": "..." } }
```

Middleware xác thực, model validation và exception handler phải dùng cùng shape này. `KeyNotFoundException` → 404, thiếu/sai quyền → 401/403, input hoặc trạng thái nghiệp vụ sai → 400, xung đột ghi đồng thời hoặc bản ghi duy nhất trùng → 409, lỗi không dự kiến → 500 với thông điệp trung tính. **Không** để raw exception/stack trace lộ ra client; log server giữ chi tiết. Webhook thanh toán có thể dùng body xác nhận riêng theo hợp đồng với cổng thanh toán.

## Pagination

Marketplace listing dùng keyset/cursor (không dùng OFFSET sâu — chậm dần khi catalog lớn). Chat history cũng cursor theo `SentAt`.

## Auth

JWT Bearer. Access token 15 phút, refresh token 7 ngày — refresh token **rotate mỗi lần dùng**, lưu hash (không lưu plaintext) trong `RefreshTokens`, phát hiện reuse → revoke toàn bộ chain (chống token bị đánh cắp).

## Real-time (SignalR)

Hub riêng: `/hubs/chat`, `/hubs/notifications`. Auth qua JWT truyền query-string lúc handshake (chuẩn SignalR, không dùng polling REST thay thế).

## Rate limiting

Dùng `Microsoft.AspNetCore.RateLimiting` built-in (.NET 8), fixed-window trên endpoint public nhạy cảm (auth, search) — chặn brute-force/scrape.

## Docs

Swashbuckle tự sinh OpenAPI từ code — endpoint mới bắt buộc có `[ProducesResponseType]`/XML doc, không viết tay spec riêng (tránh lệch).
