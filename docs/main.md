# TÀI LIỆU TỔNG HỢP TOÀN BỘ LUỒNG NGHIỆP VỤ, GIAO DIỆN & KIẾN TRÚC HỆ THỐNG DILLUSTRATION

**Dự án:** Dillustration – Art Portfolio & Commission Platform  
**Đồ án:** SEP490 Capstone Project  
**Tài liệu tham chiếu:** SRS 34 Use Cases, CSDL 24 Bảng (`Dillustration_Database_v01.sql`), NFR Benchmarks  

---

## 📌 PHẦN 1: MASTER PROMPT - TỔNG HỢP 5 PHÂN HỆ NGHIỆP VỤ & MÔ TẢ GIAO DIỆN (UI LAYOUTS)

### 🏛 I. TỔNG QUAN KIẾN TRÚC & QUY TẮC NỀN TẢNG (NON-NEGOTIABLE BENCHMARKS)

1. **Mô hình Phân quyền Dual-Role:**
   - Mọi người dùng khi đăng ký đều mang vai trò mặc định `"Client"`.
   - Khi người dùng tạo hồ sơ Họa sĩ (`CreatorProfile`), hệ thống cấp bổ sung vai trò `"Creator"`. Mảng vai trò trong JWT Token là `["Client", "Creator"]`.
2. **Phong cách Giao diện (Digital Etherealism):**
   - **Bảng màu:** Tông nền Brand Navy `#1F2335`, hiệu ứng phát sáng Periwinkle `#A7ACD9`, thẻ chứa `#131316`.
   - **Thẻ Thủy tinh (Glassmorphism):** Sử dụng class `.glass-container` với hiệu ứng phủ mờ `backdrop-filter: blur(24px)` và đường viền mảnh `border-t border-white/15`.
   - **Font chữ:** Font tiêu đề `Sora`, font nội dung `Space Grotesk`.
3. **Bảo mật & Hiệu năng Kỹ thuật (NFR Benchmark):**
   - **File gốc:** Lưu trữ trên AWS S3 Private Bucket, chỉ tải được bằng Presigned URL thời hạn 15 phút sau khi giải ngân mốc cuối.
   - **Ảnh xem thử (WIP):** Tự động đóng Watermark anti-piracy qua Cloudinary/CDN.
   - **Real-time Engine:** SignalR WebSockets đẩy tin nhắn chat Workroom và thông báo tiến độ với độ trễ < 200ms.
   - **Tính toàn vẹn tài chính:** 100% giao dịch Nạp tiền, Ký quỹ Escrow (`HELD`), Giải ngân mốc và Rút tiền (`Wallet`) tuân thủ tính nguyên tố ACID qua Database Transaction.

---

### 🔄 II. CHI TIẾT 5 PHÂN HỆ NGHIỆP VỤ & MÔ TẢ GIAO DIỆN (UI LAYOUTS)

#### PHÂN HỆ 1: XÁC THỰC, ĐĂNG KÝ & QUẢN LÝ TÀI KHOẢN (AUTH & IDENTITY)
*Bao gồm Use Cases: UC01 (Register & OTP), UC02 (Authenticate & Session), UC03 (Forgot Password), UC04 (Manage Profile).*

##### 1. Luồng Nghiệp vụ (Business Workflows):
- **Đăng ký & Xác thực OTP (UC01):** Khách hàng nhập Email, Mật khẩu, Họ tên -> Hệ thống tạo tài khoản `Users` (vai trò `"Client"`), gửi mã OTP 6 số qua Transactional Email -> Người dùng nhập OTP -> Kích hoạt `EmailConfirmed = true` và `IsVerified = true`.
- **Đăng nhập & Phiên làm việc (UC02):** Người dùng nhập Email/Password hoặc chọn OAuth Google/Facebook -> Hệ thống xác thực, trả về JWT Access Token (hạn 15-60 phút) và HttpOnly Refresh Token -> Lưu state vào Zustand Auth Store. Hỗ trợ nhận dạng Dual-Role (`Client` + `Creator`).
- **Quên mật khẩu (UC03):** Nhập Email -> Nhận Email chứa Token khôi phục -> Nhập Mật khẩu mới -> Hệ thống mã hóa BCrypt/Argon2id và cập nhật `PasswordHash`.
- **Quản lý Hồ sơ & Bảo mật (UC04):** Đổi thông tin cá nhân, Avatar, Mật khẩu, Bật xác thực 2 yếu tố (2FA), Xem danh sách thiết bị/Refresh Tokens đang hoạt động và Đăng xuất từ xa.

##### 2. Mô tả Giao diện CHI TIẾT (UI Layout Description):
- 🖥️ **Trang Đăng ký (`/register`):**
  - Khung chứa Glassmorphism căn giữa màn hình trên nền Navy tối có hạt sao lung linh (`#cursor-glow`).
  - Logo Dillustration `/weblogo.png` sáng ở trên; Form nhập Họ tên, Email, Mật khẩu, Nhập lại mật khẩu; Nút "Tạo tài khoản Client" màu tím neon; Nút Đăng ký nhanh bằng Google/Facebook OAuth; Dòng liên kết "Đã có tài khoản? Đăng nhập".
- 🖥️ **Trang Nhập OTP (`/verify-otp`):**
  - Modal/Trang đơn giản chứa biểu tượng Hòm thư bảo mật; 6 ô nhập mã OTP tự động focus; Đồng hồ đếm ngược resend OTP (60s); Nút "Xác thực & Kích hoạt".
- 🖥️ **Trang Đăng nhập (`/login`):**
  - Card Glassmorphism đồng bộ; Form nhập Email, Password; Checkbox "Ghi nhớ phiên đăng nhập"; Liên kết "Quên mật khẩu?"; Nút Submit "Đăng nhập"; Nút OAuth Login.
- 🖥️ **Trang Cài đặt Tài khoản (`/profile/settings`):**
  - Thanh Tab nằm ngang: `[Hồ sơ cá nhân]` | `[Bảo mật & Mật khẩu]` | `[Xác thực 2FA]` | `[Phiên đăng nhập]`.
  - Tab Hồ sơ: Avatar upload dạng tròn kèm preview, Form sửa FullName, Bio, Số điện thoại.
  - Tab Bảo mật: Form đổi mật khẩu cũ/mới, Danh sách các thiết bị/IP đang dùng Refresh Token kèm nút "Đăng xuất khỏi thiết bị này".

---

#### PHÂN HỆ 2: KHÁM PHÁ MARKETPLACE & HỒ SƠ HỌA SĨ (MARKETPLACE & PORTFOLIO)
*Bao gồm Use Cases: UC05 (Browse Feed), UC06 (Search & Filter), UC07 (Artwork Detail), UC08 (Creator Profile & Rate Card), UC09 (Follow Creator), UC19 (Configure Rate Card), UC20 (Upload Artwork), UC21 (Manage Portfolio).*

##### 1. Luồng Nghiệp vụ (Business Workflows):
- **Khám phá & Gợi ý AI (UC05):** Người dùng truy cập Trang chủ -> Xem Bảng tin Feed chứa các Artwork mới nhất, nổi bật hoặc được đề xuất cá nhân hóa qua AI Recommendation.
- **Tìm kiếm & Bộ lọc nâng cao (UC06):** Tìm kiếm theo Từ khóa, Phong cách (`Style`: Anime, Pixel Art, Digital Painting, Chibi), Thẻ tag (`Tag`), Khoảng giá dịch vụ, và Toggle lọc cờ AI (`IsAiGenerated`). Thời gian phản hồi API < 500ms.
- **Xem Chi tiết Artwork & Tag AI (UC07):** Click Artwork -> Mở chi tiết xem ảnh xem thử nén WebP có watermark, xem mô tả, Tác giả, và Danh sách thẻ tag tự động chiết xuất bởi AI Cloud API (`Google Cloud Vision` / `OpenAI`).
- **Xem Profile Họa sĩ & Rate Card (UC08):** Xem Hồ sơ công khai Creator (`CreatorProfile`), Bio, Đánh giá trung bình (`RatingAvg`), Huy hiệu xác minh vẽ tay (`IsAiVerified`), Bảng giá (`RateCard` JSON) và Số lượng đơn khả dụng (`CommissionSlots`).
- **Theo dõi Creator (UC09):** Bấm Follow/Unfollow -> Ghi nhận vào bảng `Follow` -> Cập nhật danh sách `Following Creators` trong Client Dashboard.
- **Cấu hình Bảng giá & Gói Dịch vụ (UC19):** Creator tạo các gói dịch vụ (`CommissionService`) gồm Tiêu đề, Loại hình art, Mô tả chi tiết, Giá khởi điểm, và Số slot nhận vẽ đồng thời.
- **Đăng tác phẩm & Quản lý Portfolio (UC20, UC21):** Creator tải ảnh tác phẩm mẫu -> Hệ thống tự động gửi AI Scan (Lọc NSFW, Gắn AI Tag tự động, Tạo thumbnail WebP và đóng Watermark CDN) -> Đăng bài lên Portfolio (`Artwork`).

##### 2. Mô tả Giao diện CHI TIẾT (UI Layout Description):
- 🖥️ **Trang chủ Marketplace (`/`):**
  - **Header Session:** Logo thương hiệu `/weblogo.png`, Thanh tìm kiếm nhanh, Menu điều hướng, Nút Thông báo (chuông real-time), Avatar User với Dropdown xem vai trò Dual-Role.
  - **Hero Section:** Banner Etherealism với ánh sáng Periwinkle tỏa sáng theo con trỏ chuột (`#cursor-glow`), Floating Showcase tác phẩm nghệ thuật xu hướng.
  - **Category Tabs & Grid:** Thanh chuyển Category (All, Anime, Realistic, Concept Art, Fantasy), Bento Grid hiển thị các Card `Artwork` hiệu ứng Kính mờ (Glassmorphism), hover hiển thị Tên tác phẩm, Tên Creator và lượt thích.
- 🖥️ **Trang Tìm kiếm & Lọc (`/search`):**
  - **Bố cục:** 2 Cột (Sidebar Lọc trái 25% - Results Grid phải 75%).
  - **Sidebar Left:** Slider khoảng giá VND/USD, Checkbox chọn Style, Cloud Tag Pills, Toggle "Ẩn các tác phẩm AI (`IsAiGenerated`)".
  - **Results Right:** Lưới tác phẩm khớp điều kiện tìm kiếm, hỗ trợ phân trang hoặc Load More.
- 🖥️ **Màn hình Chi tiết Artwork (`/artwork/[id]`):**
  - **Bố cục Modal / Page:** Ảnh tác phẩm chính hiển thị lớn ở trung tâm (có Watermark anti-piracy).
  - **Sidebar Right:** Avatar + DisplayName của Creator, Huy hiệu `IsAiVerified`, Nút "Follow", Nút "Đặt vẽ Commission ngay", Khung Mô tả tác phẩm, Danh sách các thẻ `Tag` badge màu tím mờ.
- 🖥️ **Trang Profile Họa sĩ (`/creator/[id]`):**
  - **Header Profile:** Cover Banner phía trên; Avatar tròn, DisplayName, Bio, Điểm đánh giá ⭐ `RatingAvg` (vd: 4.9/5) ở giữa.
  - **Navigation Tabs:** `[Portfolio Gallery]` | `[Bảng giá / Rate Card]` | `[Gói Commission]` | `[Đánh giá từ Khách]`.
  - **Tab Rate Card:** Các thẻ Gói dịch vụ thiết kế dạng Bảng giá chỉn chu, hiển thị Giá cố định, Số slot khả dụng (`CommissionSlots: 3/5 remaining`), và Nút "Chọn gói này".
- 🖥️ **Trang Quản lý Portfolio Creator (`/creator/dashboard/portfolio`):**
  - Nút "Tải lên Tác phẩm Mới" góc phải; Lưới hiển thị danh sách Artwork hiện có kèm nút Sửa/Xóa/Ẩn bài.
  - Modal Upload: Khung Drag-and-drop ảnh tác phẩm, Form nhập Tiêu đề, Mô tả, Chọn Phong cách (Style), Checkbox "Tác phẩm có sử dụng công cụ AI (`Tag.IsAiGenerated`)".

---

#### PHÂN HỆ 3: QUY TRÌNH COMMISSION 4 MỐC & PHÒNG LÀM VIỆC REAL-TIME (COMMISSION ENGINE & WORKROOM)
*Bao gồm Use Cases: UC10 (Submit Request), UC13 (Track Orders), UC14 (Approve Milestone), UC15 (Download Final), UC16 (Review & Rating), UC22 (Process Request), UC23 (Submit WIP), UC24 (Deliver Final), UC26 (SignalR Workroom Chat).*

##### 1. Luồng Nghiệp vụ (Business Workflows):
- **Gửi Yêu cầu Đặt vẽ (UC10):** Client chọn gói dịch vụ hoặc đặt vẽ từ Profile Creator -> Nhập Mô tả yêu cầu, Tải ảnh tham khảo (Reference Images), Hạn nộp bài mong muốn -> Tạo `Commission` ở trạng thái `Created`.
- **Duyệt & Chia mốc Đơn hàng (UC22):** Creator xem yêu cầu -> Chấp nhận/Từ chối -> Nếu nhận đơn, Creator thiết lập các mốc tiến độ (`Milestone`: Sketch, Lineart, Color, Final) với các trường `Sequence`, `Price`, `RevisionLimit` (Giới hạn số lần sửa).
- **Thanh toán Ký quỹ Escrow (UC11):** Client nhận thông báo -> Thực hiện chuyển tiền cọc Ký quỹ qua Cổng thanh toán (VNPay/MoMo) hoặc Ví Dillustration -> Tiền khóa ở trạng thái `HELD` trong `EscrowTransaction` -> Kích hoạt `Commission Workroom`.
- **Thực hiện, Nộp & Duyệt WIP từng mốc (UC23, UC14):**
  - Creator tải lên ảnh phác thảo xem thử có đóng watermark (`WipPreviewUrl`).
  - Client nhận thông báo tức thì qua SignalR (<200ms) -> Xem bài -> Bấm "Duyệt Mốc này" (Approve) hoặc "Yêu cầu Sửa bài" (Request Revision - nếu `RevisionCount < RevisionLimit`).
  - Khi Client duyệt mốc, tiền Escrow của mốc đó tự động giải ngân chuyển thành số dư khả dụng trong `Wallet` của Creator.
- **Bàn giao File gốc & Tải xuống (UC24, UC15):** Creator nộp mốc cuối cùng kèm File gốc dung lượng cao (.PSD, .SAI, .PNG nén) -> Client duyệt mốc cuối -> Hệ thống giải ngân phần tiền còn lại -> Mở khóa **AWS S3 Presigned URL (hạn 15 phút)** để Client tải File gốc an toàn.
- **Đánh giá Creator (UC16):** Đơn hàng thành công (`Completed`) -> Client gửi đánh giá 1-5 sao và nhận xét (`Review`), tự động tính lại `RatingAvg` cho Creator.
- **Phòng làm việc & Chat Real-time (UC26):** Client và Creator trao đổi tin nhắn 1-1, gửi ảnh tham khảo trực tiếp trong Workroom (`Message`), đồng bộ tức thì qua SignalR Hub.

##### 2. Mô tả Giao diện CHI TIẾT (UI Layout Description):
- 🖥️ **Form Tạo Yêu cầu Commission (`/commission/create`):**
  - Form Wizard từng bước: Bước 1: Chọn gói vẽ & Tiêu đề; Bước 2: Mô tả ý tưởng & Drag-and-drop ảnh tham khảo; Bước 3: Xác nhận hạn giao & Điều khoản -> Nút "Gửi Yêu cầu Đặt vẽ".
- 🖥️ **Màn hình Danh sách Đơn hàng (`/client/commissions` & `/creator/commissions`):**
  - Thanh Tab trạng thái: `[Tất cả]` | `[Chờ Creator duyệt]` | `[Chờ cọc Escrow]` | `[Đang vẽ]` | `[Hoàn thành]` | `[Tranh chấp]`.
  - Bảng/Lưới Đơn hàng: Thumbnail ảnh, Tiêu đề đơn, Tên đối tác, Tổng tiền, Trạng thái mốc hiện tại, Nút "Vào phòng làm việc (Workroom)".
- 🖥️ **PHÒNG LÀM VIỆC CHUYÊN NGHIỆP (Commission Workroom UI `/workroom/[id]`):**
  - **Top Bar (Timeline Stepper):** Thanh tiến trình hiển thị trực quan 4 mốc (Ví dụ: `1. Phác thảo 🟢` ➔ `2. Nét (Lineart) 🟡 (Đang làm)` ➔ `3. Màu (Color) ⚪` ➔ `4. Bàn giao ⚪`). Cập nhật trạng thái thời gian thực qua SignalR.
  - **Left Column (Chat Panel - 35% chiều rộng):**
    - Lịch sử tin nhắn trao đổi 1-1 giữa Client và Creator.
    - Khung nhập tin nhắn kèm nút đính kèm tài liệu/ảnh tham khảo, indicator "Creator đang gõ...".
  - **Right Column (Workspace & Deliverables - 65% chiều rộng):**
    - Khung xem ảnh WIP phác thảo lớn (có hiển thị Watermark mờ).
    - Box thông tin Mốc hiện tại: Tên mốc, Giá mốc, Số lần sửa bài (`RevisionCount: 1 / 3`).
    - **Nút hành động chính:**
      - *Phía Client:* Nút "Duyệt mốc này & Giải ngân", Nút "Yêu cầu sửa bài", Nút "Gửi Khiếu nại (Raise Dispute)".
      - *Phía Creator:* Nút "Upload ảnh WIP mốc này", Nút "Bàn giao File gốc sản phẩm".
      - *Khi hoàn tất:* Hiển thị Nút "Tải xuống File gốc (AWS S3 Presigned Link)" & Popup "Đánh giá & Review Creator".

---

#### PHÂN HỆ 4: THANH TOÁN KÝ QUỸ, VÍ ĐIỆN TỬ & DÒNG TIỀN (ESCROW, WALLET & PAYMENT)
*Bao gồm Use Cases: UC11 (Deposit Escrow), UC17 (View Wallet & History), UC25 (Request Payout), UC33 (Configure Fee & Policies).*

##### 1. Luồng Nghiệp vụ (Business Workflows):
- **Thanh toán Ký quỹ Escrow (UC11):** Khách nạp tiền cọc đơn hàng -> Kết nối Cổng thanh toán (VNPay / MoMo) qua RESTful API HTTPS -> Nhận Webhook IPN xác thực chữ ký HMAC-SHA256 -> Ghi nhận `Payment` -> Khóa tiền vào `EscrowTransaction` (trạng thái `HELD`).
- **Xem Ví & Lịch sử Giao dịch (UC17):** Người dùng xem Số dư khả dụng (`Wallet.Balance`), Số dư tạm giữ Escrow, và Sổ cái biến động tiền tệ (`Transaction`).
- **Yêu cầu Rút tiền Ngân hàng (UC25):** Creator nhập số tiền muốn rút và Thông tin ngân hàng (Số tài khoản, Tên chủ tài khoản, Tên ngân hàng) -> Tạo `PayoutRequest` (trạng thái `Pending`) -> Trừ số dư khả dụng ví -> Admin duyệt -> Chuyển khoản -> Cập nhật trạng thái `ProcessedAt`.
- **Cấu hình Phí sàn & Chính sách (UC33):** Admin thiết lập tỷ lệ phí nền tảng (Platform Fee Rate, vd: 5% - 10%), quy tắc hoàn tiền và chính sách giải ngân.

##### 2. Mô tả Giao diện CHI TIẾT (UI Layout Description):
- 🖥️ **Màn hình Thanh toán Escrow (`/payment/checkout/[id]`):**
  - Card tóm tắt Đơn hàng (Tiêu đề, Tên Creator, Tổng tiền cọc Escrow).
  - Lựa chọn Cổng thanh toán: `[VNPay QR]` | `[Ví MoMo]` | `[Số dư Ví Dillustration]`.
  - Khung cam kết an toàn: "Số tiền của bạn sẽ được giữ an toàn tại Ví Escrow và chỉ giải ngân khi bạn bấm duyệt từng mốc sản phẩm." -> Nút "Xác nhận Thanh toán Ký quỹ".
- 🖥️ **Trang Quản lý Ví & Dòng tiền (`/wallet`):**
  - **Bảng Thống kê (Metric Cards):** Card 1: Số dư khả dụng (Số tiền màu tím neon lớn); Card 2: Số tiền đang tạm giữ Escrow; Card 3: Tổng doanh thu đã rút.
  - **Nút Hành động:** Nút "Rút tiền về Ngân hàng" (Mở Modal nhập STK, Ngân hàng, Số tiền rút).
  - **Bảng Lịch sử Giao dịch (`Transaction` Log Table):** Hiển thị chi tiết từng dòng: Mã GD, Ngày giờ, Loại giao dịch (Nạp tiền, Cọc Escrow, Nhận giải ngân mốc, Phí sàn, Rút tiền), Số tiền biến động (+/- màu xanh/đỏ), và Trạng thái.

---

#### PHÂN HỆ 5: ANTI-AI, KIỂM DUYỆT & XỬ LÝ TRANH CHẤP (ANTI-AI, MODERATION & DISPUTE)
*Bao gồm Use Cases: UC27 (Raise Dispute), UC28 (Review Content & AI Scan), UC29 (Review Creator Verification), UC30 (Arbitrate Dispute), UC31 (Manage Users & Sanctions), UC32 (Admin Overview & Financial KPIs).*

##### 1. Luồng Nghiệp vụ (Business Workflows):
- **Ghi nhãn & Kiểm duyệt AI tự động (UC28):** Khi Artwork đăng tải, AI Cloud API tự động quét NSFW, bản quyền và chiết xuất tag. Nếu phát hiện yếu tố AI -> Tự động gắn cờ `Tag.IsAiGenerated = true`. Nếu bài đăng vi phạm chính sách -> Chuyển vào Hàng chờ Kiểm duyệt Admin (`Artwork Moderation`).
- **Xác minh Hồ sơ Tay nghề Creator (UC29):** Creator gửi hồ sơ/video chứng minh vẽ tay thật -> Admin thẩm định -> Cấp huy hiệu `CreatorProfile.IsAiVerified = true`.
- **Khởi tạo & Phán quyết Tranh chấp (UC27, UC30):**
  - Client hoặc Creator gửi đơn `Dispute` từ Workroom -> Nhập Lý do & Bằng chứng -> Hệ thống khóa trạng thái Escrow và lưu Snapshot bằng chứng.
  - Moderator/Admin truy cập Admin Dashboard thẩm định tin nhắn chat và các mốc WIP -> Nhập ghi chú (`AdminNote`) và Tỷ lệ hoàn tiền `Dispute.RefundRate` (từ `0.0` đến `1.0`, vd: 0.6 hoàn Client, 0.4 giải ngân Creator) -> Hệ thống tự động chia tiền về Ví 2 bên và lưu `AuditLog`.
- **Quản lý Người dùng & Khóa tài khoản (UC31):** Admin xem danh sách User, khóa/mở khóa tài khoản vi phạm, ghi vết hành động vào `AuditLog`.
- **Dashboard Thống kê Hệ thống (UC32):** Admin xem biểu đồ tổng doanh thu, doanh thu phí sàn, số lượng đơn commission, và tổng số người dùng.

##### 2. Mô tả Giao diện CHI TIẾT (UI Layout Description):
- 🖥️ **Form Gửi Khiếu nại Tranh chấp (`/workroom/[id]/dispute` - Modal):**
  - Form chọn Phân loại sự cố (Trễ hạn nộp bài, Sản phẩm không đúng yêu cầu, Tẩy xóa tác phẩm, Thái độ không đúng mực).
  - Khung nhập Chi tiết lý do, Khung Upload ảnh/tài liệu bằng chứng -> Nút "Gửi Đơn Khiếu nại tới Admin".
- 🖥️ **TRANG QUẢN TRỊ ADMIN DASHBOARD (`/admin`):**
  - **Sidebar Left Navigation:**
    - `[📊 Tổng quan System KPIs]`
    - `[👥 Quản lý Người dùng]`
    - `[🎨 Duyệt Hồ sơ Creator]`
    - `[🖼️ Kiểm duyệt Artwork & AI]`
    - `[⚖️ Xử lý Tranh chấp (Dispute)]`
    - `[⚙️ Cấu hình Phí & Tài chính]`
  - 🖥️ **Màn hình Phán quyết Tranh chấp (`/admin/disputes/[id]`):**
    - **Bố cục Split-Screen (Chia 2 Cột):**
      - *Cột trái (60%):* Trình xem lại Snapshot Lịch sử Chat Workroom, Các mốc WIP đã đăng, Đơn khiếu nại & Bằng chứng do 2 bên cung cấp.
      - *Cột phải (40%):* Form Phán quyết của Admin. Khung nhập Ghi chú thẩm định (`AdminNote`); **Slider chọn Tỷ lệ hoàn tiền (`RefundRate`)** kéo từ 0% Client - 100% Creator đến 100% Client - 0% Creator; Nút hành động "Thực thi Phán quyết & Giải ngân Tự động".

---

### 📑 III. MA TRẬN ĐỐI CHUYỂN 34 USE CASES VỚI CÁC MÀN HÌNH

| UC ID | Tên Use Case | Màn hình tương ứng trên Frontend Next.js | Vai trò thực hiện |
| :---: | :--- | :--- | :---: |
| **UC01** | Register & Verify Account | `/register`, `/verify-otp` | Guest |
| **UC02** | Authenticate & Manage Session | `/login` | Guest / User |
| **UC03** | Forgot / Reset Password | `/forgot-password`, `/reset-password` | Guest |
| **UC04** | Manage Profile & Security | `/profile/settings` | Client / Creator |
| **UC05** | Browse Feed & Recommendations | `/` (Marketplace Home) | Guest / Client |
| **UC06** | Search Artworks with Filters | `/search` | Guest / Client |
| **UC07** | View Artwork Details | `/artwork/[id]` | Guest / Client |
| **UC08** | View Creator Profile & Rate Card| `/creator/[id]` | Guest / Client |
| **UC09** | Follow / Unfollow Creator | `/creator/[id]`, `/client/following` | Client |
| **UC10** | Submit Commission Request | `/commission/create` | Client |
| **UC11** | Deposit Escrow Funds | `/payment/checkout/[id]` | Client |
| **UC12** | View Client Dashboard | `/client/dashboard` | Client |
| **UC13** | Track Commission Orders | `/client/commissions` | Client |
| **UC14** | Review & Approve Milestone | `/workroom/[id]` | Client |
| **UC15** | Download Final Artwork | `/workroom/[id]` | Client |
| **UC16** | Submit Creator Review & Rating | `/workroom/[id]` (Review Modal) | Client |
| **UC17** | View Wallet & History | `/wallet` | Client / Creator |
| **UC18** | View Creator Dashboard | `/creator/dashboard` | Creator |
| **UC19** | Configure Rate Card & Slots | `/creator/dashboard/rate-card` | Creator |
| **UC20** | Upload Artwork to Portfolio | `/creator/dashboard/portfolio` (Upload Modal) | Creator |
| **UC21** | Manage Portfolio Inventory | `/creator/dashboard/portfolio` | Creator |
| **UC22** | Process Commission Request | `/creator/commissions` | Creator |
| **UC23** | Submit Milestone Progress (WIP)| `/workroom/[id]` | Creator |
| **UC24** | Deliver Final Deliverables | `/workroom/[id]` | Creator |
| **UC25** | Request Payout | `/wallet` (Payout Modal) | Creator |
| **UC26** | Chat & Collaborate in Workroom| `/workroom/[id]` | Client & Creator |
| **UC27** | Raise Dispute Request | `/workroom/[id]/dispute` | Client & Creator |
| **UC28** | Review Content & AI Flagged Art| `/admin/moderation` | Admin / Moderator |
| **UC29** | Review Creator Application | `/admin/creator-verifications` | Admin |
| **UC30** | Arbitrate Dispute & Escrow | `/admin/disputes/[id]` | Admin |
| **UC31** | Manage User Accounts | `/admin/users` | Admin |
| **UC32** | View System Overview KPIs | `/admin` | Admin |
| **UC33** | Configure Fee Rates & Policies | `/admin/finance-settings` | Admin |
| **UC34** | Real-time Push Notifications | Topbar Notification Center (System-wide) | All Users |

---

## ⚡ PHẦN 2: PHÂN TÍCH HIỆU NĂNG REAL-TIME CHAT TRONG WORKROOM (SIGNALR PERFORMANCE & BOTTLENECK ANALYSIS)

### ❓ Thắc mắc: 
> *Mở Chat Realtime mặc định tại Workroom UI (`/workroom/[id]`) như vậy có khiến Server bị nghẽn hoặc lag không?*

### 🟢 Trả lời ngắn gọn:
**KHÔNG BỊ NGHẼN HOẶC LAG**, nếu triển khai đúng chuẩn kiến trúc của **ASP.NET Core 8 SignalR Hub**.

### 🔍 Phân tích kỹ thuật chi tiết:

#### 1. Đặc thù phòng Chat Workroom là 1-1 (Client & Creator)
- Workroom **không phải là phòng Chat Livestream công cộng** (nơi 10.000 người cùng nhắn vào 1 luồng).
- Mỗi Workroom chỉ có **đúng 2 kết nối đồng thời** (1 Client + 1 Creator). Dù hệ thống có 1.000 người dùng online cùng lúc (1.000 CCU), hệ thống chỉ chịu khoảng **500 phòng chat độc lập**, lưu lượng dữ liệu (throughput) phân tán cực kỳ nhỏ.

#### 2. Hiệu năng vượt trội của ASP.NET Core 8 SignalR
- ASP.NET Core 8 sử dụng mô hình **Asynchronous Non-blocking I/O (epoll/IOCP)**.
- Một kết nối WebSocket duy trì ở trạng thái rảnh (Idle Connection) chỉ tốn khoảng **2 KB - 4 KB RAM**.
- Một server ASP.NET Core cơ bản (RAM 4GB, 2 vCPU) có khả năng duy trì ổn định **10.000 đến 50.000 kết nối WebSocket đồng thời** mà không bị quá tải CPU hay RAM.

#### 3. 3 Rủi ro duy nhất gây lag (nếu viết code sai) và Giải pháp khắc phục:

| Rủi ro kỹ thuật | Nguyên nhân code sai | Giải pháp khắc phục chuẩn Production |
| :--- | :--- | :--- |
| 🔴 **Nghẽn RAM/Bandwidth do File lớn** | Convert ảnh/file .PSD 50MB - 100MB thành chuỗi Base64 rồi đẩy trực tiếp qua SignalR Hub. | **Phân tách luồng:** Chỉ gửi tin nhắn text qua SignalR JSON. Upload file lớn trực tiếp từ Browser lên S3/Cloudinary qua **Direct Presigned Upload**, sau đó chỉ truyền URL ảnh qua SignalR. |
| 🔴 **Nghẽn CPU do Broadcast sai Scope** | Phát tin nhắn chat của 1 phòng tới toàn bộ người dùng trên sàn (`Clients.All`). | **Gom nhóm SignalR Groups:** Sử dụng `Groups.AddToGroupAsync(Context.ConnectionId, $"workroom-{commissionId}")` và gửi tin nhắn qua `Clients.Group(...)`. |
| 🔴 **Nghẽn DB do Polling/Typing Log** | Mỗi lần gõ chữ ("User đang gõ...") lại gọi `INSERT` câu lệnh SQL Server. | **Bất đồng bộ DB Write:** Tin nhắn hiển thị tức thì qua WebSocket (<200ms). Việc lưu `Message` vào SQL Server dùng luồng `async / await` không block thread hoặc dùng Background Worker. |

---

## 🧭 PHẦN 3: CHI TIẾT SIDEBAR CHO DUAL-ROLE (CLIENT & CREATOR SIDEBAR)

Do Dillustration áp dụng cơ chế phân quyền **Dual-Role (`["Client", "Creator"]`)**, giao diện Sidebar được thiết kế linh hoạt cho từng chế độ làm việc:

### 🔵 1. Sidebar dành cho Client (Khách hàng / Buyer)
> **Trạng thái:** Tông màu Navy - Xanh lá (`/client/*`)

* 📊 **Tổng quan (Client Dashboard):** `/client/dashboard` - Xem chỉ số chi tiêu, đơn hàng đang chạy (`UC12`).
* 🎨 **Đơn hàng của tôi (My Commissions):** `/client/commissions` - Danh sách đơn đặt vẽ, lọc theo các tab trạng thái (`UC13`).
* 💬 **Phòng làm việc (Workroom):** `/workroom/[id]` - Vào trực tiếp phòng làm việc 1-1 với Họa sĩ (`UC14`, `UC15`, `UC26`).
* 💖 **Họa sĩ đang theo dõi (Following Creators):** `/client/following` - Xem danh sách Creator đã Follow (`UC09`).
* 💳 **Ví & Lịch sử giao dịch (Wallet & Payments):** `/wallet` - Quản lý tiền cọc Escrow, lịch sử giao dịch (`UC11`, `UC17`).
* ⚙️ **Cài đặt tài khoản (Account Settings):** `/profile/settings` - Cài đặt cá nhân, đổi mật khẩu, 2FA (`UC04`).

---

### 🟡 2. Sidebar dành cho Creator (Họa sĩ / Artist)
> **Trạng thái:** Tông màu Navy - Vàng da cam (`/creator/*`)

* 📊 **Tổng quan Họa sĩ (Creator Overview):** `/creator/dashboard` - Phân tích doanh thu, điểm rating ⭐, slot trống (`UC18`).
* 📦 **Quản lý Đơn nhận vẽ (Commission Orders):** `/creator/commissions` - Xem yêu cầu vẽ từ Khách, chấp nhận/từ chối đơn & chia mốc (`UC22`).
* 🖼️ **Bộ sưu tập Portfolio (Portfolio Inventory):** `/creator/dashboard/portfolio` - Upload bài vẽ mẫu (`UC20`), ẩn/hiện tác phẩm (`UC21`).
* 🏷️ **Bảng giá & Gói dịch vụ (Rate Card & Services):** `/creator/dashboard/rate-card` - Cấu hình bảng giá `RateCard` JSON & số Slot nhận vẽ (`UC19`).
* 💰 **Ví & Rút tiền Ngân hàng (Wallet & Payouts):** `/wallet` - Quản lý số dư khả dụng & Tạo yêu cầu rút tiền về Ngân hàng (`UC25`, `UC17`).
* 👁️ **Xem Profile công khai (Public Profile):** `/creator/[id]` - Xem trước trang công khai mà Khách xem (`UC08`).
* ⚙️ **Cài đặt Hồ sơ Họa sĩ (Creator Settings):** `/creator/dashboard/settings` - Nộp minh chứng vẽ tay xin huy hiệu `IsAiVerified` (`UC29`).

---

### 🔄 3. Nút chuyển đổi Dual-Role (Role Switcher)
Nằm ở **dưới cùng Sidebar** (hoặc Avatar Dropdown Header):
- Khi ở chế độ Client: Nút 🟢 `[Chuyển sang Giao diện Họa sĩ (Creator Mode)]`
- Khi ở chế độ Creator: Nút 🔵 `[Chuyển sang Giao diện Khách hàng (Client Mode)]`

---

## 📊 PHẦN 4: CHI TIẾT DASHBOARD CHO CLIENT & CREATOR (DASHBOARD SPECIFICATIONS)

### 🔵 1. Client Dashboard (`UC12` - Route: `/client/dashboard`)

#### Các thành phần UI chính:
1. **Metric Cards (Chỉ số nhanh):**
   - 🎨 **Đơn đặt vẽ đang chạy (Active Commissions):** Số đơn đang vẽ/chờ cọc (vd: `3 đơn`).
   - 💰 **Tổng chi tiêu (Total Spent):** Tổng số tiền đã thanh toán qua Escrow cho các đơn thành công (vd: `15.500.000 VNĐ`).
   - 💖 **Họa sĩ đang theo dõi (Following Creators):** Số Họa sĩ đã bấm Follow (vd: `12 Creators`).
   - ⏳ **Mốc chờ duyệt (Pending Milestones):** Số mốc WIP đang chờ nghiệm thu.
2. **Active Orders Widget (Đơn hàng đang hoạt động):**
   - Lưới đơn đang vẽ kèm **Thanh Stepper thu nhỏ** (vd: *Đang ở Mốc 2: Lineart*), thumbnail ảnh WIP mới nhất, tên Creator và nút **"Vào phòng làm việc (Workroom)"**.
3. **Saved Creators Alerts (Thông báo Slot mới):**
   - Nhắc nhở khi Họa sĩ yêu thích mở thêm Slot nhận vẽ mới (`CommissionSlots > 0`).
4. **Recent Transactions (Lịch sử giao dịch gần đây):**
   - Tóm tắt 5 giao dịch nạp tiền/ký quỹ Escrow mới nhất.

---

### 🟡 2. Creator Dashboard (`UC18` - Route: `/creator/dashboard`)

#### Các thành phần UI chính:
1. **Business Metric Cards (Chỉ số kinh doanh):**
   - 💵 **Doanh thu tháng này (Monthly Revenue):** Tổng tiền thực nhận từ mốc đã giải ngân (vd: `28.400.000 VNĐ`).
   - 📦 **Đơn hàng chờ xử lý (Pending Requests):** Số lượng yêu cầu vẽ mới Khách gửi tới cần Duyệt/Từ chối (vd: `5 yêu cầu`).
   - ⭐ **Điểm đánh giá trung bình (Rating Avg):** Uy tín từ đánh giá của khách hàng (vd: `4.95 / 5.0` từ 42 reviews).
   - 🎯 **Slot vẽ còn trống (Active Slots):** Tình trạng slot khả dụng (vd: `Còn trống 2 / 5 slots`).
2. **Analytics Chart (Biểu đồ doanh thu & đơn hàng):**
   - Biểu đồ trực quan hóa biến động doanh thu theo tuần/tháng và số lượng mốc hoàn thành.
3. **Incoming Requests Widget (Tiếp nhận đơn mới):**
   - Danh sách đơn Khách gửi đến kèm Tiêu đề, Ngân sách đề xuất, Nút bấm nhanh **"Chấp nhận & Chia mốc"** hoặc **"Từ chối"**.
4. **Ongoing Workflows (Tiến độ các đơn đang vẽ):**
   - Nhắc nhở sắp đến hạn nộp bài WIP (Sketch/Line/Color) cho từng mốc.
5. **Portfolio Engagement (Tương tác Portfolio):**
   - Thống kê lượt xem (Views), lượt thả tim (Likes) bài vẽ trên trang công khai.

---

### 🟢 So sánh Tóm tắt 2 Dashboard:

| Tiêu chí | Client Dashboard (`/client/dashboard`) | Creator Dashboard (`/creator/dashboard`) |
| :--- | :--- | :--- |
| **Trọng tâm thiết kế** | Theo dõi tiến độ đơn mua & Quản lý chi tiêu | Quản lý doanh thu, Phản hồi đơn & Đăng bài |
| **Metric hiển thị** | Số đơn đang mua, Tổng chi tiêu, Mốc chờ duyệt | Doanh thu tháng, Đơn chờ duyệt, ⭐ Rating, Slot trống |
| **Hành động nhanh** | Nạp cọc Escrow, Duyệt mốc WIP, Chat Workroom | Chấp nhận/Từ chối đơn, Upload WIP, Rút tiền Ngân hàng |
