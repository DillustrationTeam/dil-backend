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

Dùng `ProblemDetails` (RFC7807) built-in ASP.NET Core. FluentValidation error + business exception đều map qua đây. **Không** để raw exception/stack trace lộ ra client — log đầy đủ qua Serilog, client chỉ nhận `title`/`detail`/`traceId`.

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
