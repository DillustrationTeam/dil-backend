# Danh sách Actor & Specs Use Cases — ArtCommission (Dillustration)

## 1. Bối cảnh dự án & Danh sách Actor chuẩn hóa

### 👥 7 Actor đã được xác định của hệ thống:

1. **Administrator** (Primary Human Actor): Quản trị viên hệ thống (cấu hình, quản lý người dùng, xem system logs, xử lý các ca tranh chấp mức cuối).
2. **Moderator** (Primary Human Actor): Kiểm duyệt viên (duyệt bài viết vi phạm, xử lý báo cáo từ người dùng, giải quyết tranh chấp đơn đặt vẽ/Dispute).
3. **Artist / Creator** (Primary Human Actor): Họa sĩ / Người sáng tạo (tạo hồ sơ portfolio, đăng sản phẩm, tạo các gói dịch vụ đặt vẽ - Commission Services, nhận yêu cầu đặt vẽ, nộp sản phẩm từng mốc - Milestones).
4. **Client / Commissioner** (Primary Human Actor): Khách hàng (tìm kiếm artist, gửi yêu cầu đặt vẽ, thanh toán tạm giữ Escrow, xem preview watermark, duyệt mốc & nhận file gốc).
5. **Guest** (Primary Human Actor): Người dùng vãng lai chưa đăng nhập (xem portfolio công khai, tìm kiếm tranh/artist, đăng ký, đăng nhập).
6. **Payment Gateway Service** (Secondary / External System Actor): Cổng thanh toán bên ngoài (VNPAY / MoMo) xử lý các giao dịch nạp/rút/hoàn tiền và phản hồi IPN Callback.
7. **AI Assistant Service** (Secondary / External System Actor): Dịch vụ AI bên ngoài / Microservice (phục vụ Auto-tagging tự động đánh thẻ tranh, AI Moderation quét ảnh nhạy cảm/bản quyền, và AI Recommendation gợi ý nội dung).

> **Lưu ý về mô hình User**: Hệ thống hỗ trợ tài khoản đa vai trò (Multi-role User): 1 tài khoản Registered User có thể vừa đóng vai trò làm Client (đi đặt vẽ) vừa đóng vai trò làm Artist / Creator (nhận commission).

---

## 2. Danh sách Use Cases chi tiết (2.2.2 Descriptions)

| ID | Use Case | Actors | Description |
|---|---|---|---|
| 01 | Register & Verify Account | Guest | This use case allows a new user to create a new account by providing the required credentials, which are then validated by the system. After successful verification, the account is activated. |
| 02 | Authenticate & Manage Session | Guest, Client, Creator, Moderator, Administrator | This use case allows users to securely log in to their account and access authorized features. The system then validates the credentials, manages their active session, and allows them to log out or maintain their authenticated state safely. |
| 03 | Forgot / Reset Password | Guest, Client, Creator | This use case allows users to reset their password in case they forgot it. The system verifies the user’s identity before granting them permission to create a new password and regain access to their account. |
| 04 | Manage Profile & Security Settings | Client, Creator | This use case allows users to view and update their profile information and security settings. |
| 05 | Browse Feed & Recommendations | Guest, Client, AI Assistant Service | This use case allows users to browse content through their feed and discover recommended content based on users’ behaviors and preferences. |
| 06 | Search Artworks with Advanced Filters | Guest, Client | This use case allows users to search for artworks that match their preferences by applying advanced filters. |
| 07 | View Artwork Details | Guest, Client | This use case allows users to view detailed information of an artwork (image, creator, tags,...). |
| 08 | View Creator Public Profile & Rate Card | Guest, Client | This use case allows users to view a creator’s public profile, portfolio, and rate card. |
| 09 | Follow / Unfollow Creator | Client | This use case allows a client to follow or unfollow a creator to receive updates on their portfolio and activity. |
| 10 | Submit Commission Request | Client | This use case allows a client to submit a commission request to a creator with specific requirements and specifications. |
| 11 | Deposit Escrow Funds | Client, Payment Gateway Service | This use case allows a client to deposit funds into the escrow account via external payment gateways to initiate the commission process. |
| 12 | View Client Dashboard & Metrics | Client | This use case allows a client to view their dashboard containing metrics, active commissions, and status overview. |
| 13 | Track Commission Orders (Client) | Client | This use case allows a client to track the status, progress, and history of their commission orders. |
| 14 | Review & Approve Milestone Deliverable | Client | This use case allows a client to review work-in-progress deliverables submitted for a milestone and approve or request revisions. |
| 15 | Download Final Completed Artwork | Client | This use case allows a client to download high-resolution unwatermarked final artwork deliverables upon commission completion. |
| 16 | Submit Creator Review & Rating | Client | This use case allows a client to rate and leave a review for the creator after a commission order is completed. |
| 17 | View Wallet & Transaction History | Client, Creator | This use case allows users to view their wallet balance, transaction logs, escrow deposits, and payout history. |
| 18 | View Creator Dashboard & Analytics | Creator | This use case allows a creator to view analytics, earnings, order queues, and performance metrics. |
| 19 | Configure Rate Card & Commission Slots | Creator | This use case allows a creator to set up and manage their commission rate cards, package options, and available slots. |
| 20 | Upload Artwork to Portfolio | Creator, AI Assistant Service | This use case allows a creator to upload new artwork to their public portfolio, automatically processed for tags and moderation. |
| 21 | Manage Portfolio & Gallery Inventory | Creator | This use case allows a creator to organize, edit, hide, or delete artworks in their portfolio inventory. |
| 22 | Process Commission Request | Creator | This use case allows a creator to accept, reject, or negotiate incoming commission requests from clients. |
| 23 | Submit Milestone Progress | Creator | This use case allows a creator to submit work-in-progress previews (with watermarks) for client milestone reviews. |
| 24 | Deliver Final Deliverables | Creator | This use case allows a creator to submit the final high-resolution artwork files for the completed commission order. |
| 25 | Request Payout | Creator, Payment Gateway Service | This use case allows a creator to request payout of their available wallet funds to their external bank account or payment gateway. |
| 26 | Chat & Collaborate in Workroom | Client, Creator | This use case allows clients and creators to communicate in real-time within the dedicated commission workroom. |
| 27 | Raise Dispute Request | Client, Creator | This use case allows a client or creator to raise a formal dispute regarding a commission order for moderation intervention. |
| 28 | Review Content & AI Flagged Artworks | Moderator, AI Assistant Service | This use case allows a moderator to review reported content and artworks flagged by the AI moderation service. |
| 29 | Review Creator Application Profiles | Moderator | This use case allows a moderator to review and approve/reject creator verification applications. |
| 30 | Arbitrate Dispute & Determine Escrow Payout | Moderator | This use case allows a moderator to investigate disputes and decide escrow fund distribution between client and creator. |
| 31 | Manage User Accounts & Sanctions | Administrator | This use case allows an administrator to manage user profiles, assign roles, lock accounts, or impose sanctions. |
| 32 | View System Overview & Financial KPIs | Administrator | This use case allows an administrator to monitor system-wide analytics, platform revenue, transaction KPIs, and activity logs. |
| 33 | Configure Platform Fee Rates & Policies | Administrator | This use case allows an administrator to configure system settings, platform commission fees, and operational policies. |
| 34 | Push & Receive Real-time Notifications | Guest, Client, Creator, Moderator, Administrator | This use case allows the system to send real-time notifications to users regarding order updates, messages, or admin actions. |
