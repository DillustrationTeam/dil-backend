# Tài liệu Tích hợp Marketplace Curated Sections

Tài liệu ghi nhận quy chuẩn tích hợp API giữa Frontend Marketplace và Backend API cho 5 khu vực khám phá nội dung mới trên trang chủ:

## 1. Event Spotlight & Art tuyển chọn
- **Endpoint**: `GET /api/v1/events?page=1&pageSize=4` và `GET /api/v1/artworks`
- **Mục đích**: Hiển thị các sự kiện/cuộc thi đang mở (`Open`), chấm giải (`Judging`) kèm các tác phẩm nổi bật.

## 2. Bảng xếp hạng thịnh hành
- **Endpoint**: `GET /api/v1/artworks`
- **Mục đích**: Sắp xếp bảng xếp hạng Top 10 dựa trên lượt xem (`viewCount`) hoặc lượt yêu thích (`likeCount`).

## 3. Creator tuyển chọn (Featured Creators)
- **Endpoint**: `GET /api/v1/marketplace/creators`
- **Mục đích**: Danh sách các nhà sáng tạo có đánh giá cao (`ratingAverage`), số lượng đánh giá (`ratingCount`), người theo dõi và các slot nhận đặt vẽ còn trống (`availableSlots`).

## 4. Gợi ý theo tranh đã thích (Personalized Feed)
- **Endpoint**: `GET /api/v1/marketplace/me/favorites` (khi đã xác thực)
- **Mục đích**: Đọc danh sách tác phẩm yêu thích của người dùng để phân tích từ khóa/tag và gợi ý tác phẩm tương đồng. Khách vãng lai hiển thị trạng thái yêu cầu đăng nhập.

## 5. Khám phá theo phong cách (Style Taxonomy)
- **Mục đích**: Phân nhóm tác phẩm theo trường `tags`, `style`, `title` và liên kết với trang khám phá chi tiết `/explore?q=...`.
