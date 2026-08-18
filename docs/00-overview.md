# Tổng quan dự án — ArtCommission (Dillustration)

## Đây là gì

Nền tảng portfolio nghệ sĩ + đặt vẽ commission, thị trường Việt Nam. Đồ án tốt nghiệp (SEP490).

Đối tượng dùng (7 Actor đã chuẩn hóa của hệ thống):
1. **Administrator** (Primary Human Actor): Quản trị viên hệ thống (cấu hình, quản lý người dùng, xem system logs, xử lý các ca tranh chấp mức cuối).
2. **Moderator** (Primary Human Actor): Kiểm duyệt viên (duyệt bài viết vi phạm, xử lý báo cáo từ người dùng, giải quyết tranh chấp đơn đặt vẽ/Dispute).
3. **Artist / Creator** (Primary Human Actor): Họa sĩ / Người sáng tạo (tạo hồ sơ portfolio, đăng sản phẩm, tạo các gói dịch vụ đặt vẽ - Commission Services, nhận yêu cầu đặt vẽ, nộp sản phẩm từng mốc - Milestones).
4. **Client / Commissioner** (Primary Human Actor): Khách hàng (tìm kiếm artist, gửi yêu cầu đặt vẽ, thanh toán tạm giữ Escrow, xem preview watermark, duyệt mốc & nhận file gốc).
5. **Guest** (Primary Human Actor): Người dùng vãng lai chưa đăng nhập (xem portfolio công khai, tìm kiếm tranh/artist, đăng ký, đăng nhập).
6. **Payment Gateway Service** (Secondary / External System Actor): Cổng thanh toán bên ngoài (VNPAY / MoMo) xử lý các giao dịch nạp/rút/hoàn tiền và phản hồi IPN Callback.
7. **AI Assistant Service** (Secondary / External System Actor): Dịch vụ AI bên ngoài / Microservice (phục vụ Auto-tagging tự động đánh thẻ tranh, AI Moderation quét ảnh nhạy cảm/bản quyền, và AI Recommendation gợi ý nội dung).

> **Lưu ý về mô hình User**: Hệ thống hỗ trợ tài khoản đa vai trò (Multi-role User): 1 tài khoản Registered User có thể vừa đóng vai trò làm Client (đi đặt vẽ) vừa đóng vai trò làm Artist/Creator (nhận commission).

Chi tiết Use Case của các Actor xem tại [`docs/05-use-cases.md`](05-use-cases.md).

## Điểm khác biệt (đã chốt, đừng đề xuất lại hướng khác không hỏi trước)

- Chat real-time tích hợp sẵn (không redirect qua kênh ngoài).
- Watermark preview ngay lúc đang vẽ (work-in-progress), không chỉ ảnh final.
- 3 tính năng AI tích hợp: Auto-tagging tự động đánh thẻ tranh, AI Moderation quét ảnh nhạy cảm/bản quyền, và AI Recommendation gợi ý nội dung.

## Mốc thời gian chính

- Bắt đầu: 02/07/2026
- Review 1 (GVHD): 07/08/2026
- Bảo vệ cấp Khoa/Bộ môn: ~24/10–06/11/2026
- Bảo vệ cấp trường: ~07–20/11/2026

## Giới hạn đã chốt (đừng build ngoài phạm vi này)

- Không mua tranh trực tiếp — chỉ luồng Commission.
- Không có dispute resolution tự động qua bên thứ ba (admin/moderator xử lý thủ công).
- Chỉ web, không có app di động native.
- Chỉ nhắm thị trường Việt Nam (ngôn ngữ, cổng thanh toán nội địa).

## Nguồn tài liệu gốc

Repo này (`dil-backend`) chỉ chứa code. Tài liệu yêu cầu/thiết kế đầy đủ (SRS, mapping màn hình, Software Design Document) nằm ở repo tài liệu riêng: `D:\SEP490_Dillustration` — thư mục `02_Plan_Requirement/` và `03_Software_Design/`. Nếu không chắc 1 tính năng có trong scope hay không, kiểm tra ở đó trước, đừng đoán.

