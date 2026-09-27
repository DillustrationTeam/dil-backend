namespace ArtCommission.Application.Ai.Common;

/// <summary>
/// Kiến thức nghiệp vụ của nền tảng, đưa vào prompt hệ thống của chatbot (UC44).
///
/// VÌ SAO CẦN FILE NÀY:
/// UC44 yêu cầu chatbot "giải đáp quy trình &amp; hỗ trợ sự cố 24/7". Nhưng prompt hệ thống
/// chỉ có một câu giới thiệu nền tảng, cộng với luật "chỉ dùng dữ liệu trong ngữ cảnh,
/// không được tự nghĩ ra". Hai thứ đó cộng lại khiến bot TỪ CHỐI mọi câu hỏi dạng
/// "làm sao để đấu giá", "nạp tiền thế nào" — vì trong ngữ cảnh không có gì để trả lời,
/// mà bịa thì bị cấm. Bot trả lời đúng luật nhưng vô dụng với người dùng.
///
/// NGUYÊN TẮC GHI NỘI DUNG — phải khớp với code đang chạy:
///   1. Mọi con số là giá trị MẶC ĐỊNH có thật trong code, và nói rõ "mặc định, Admin
///      có thể đổi" vì phần lớn đọc từ PlatformConfig.
///   2. KHÔNG mô tả tính năng chưa có. Ví dụ: kênh Email/Push đã có cột trong DB nhưng
///      chưa có worker gửi, nên phần thông báo chỉ được nói "trong ứng dụng".
///      Bot nói sai về hệ thống còn tệ hơn bot nói "mình chưa rõ".
///   3. Sửa quy trình trong code thì PHẢI cập nhật file này. Đây là tài liệu sống.
///
/// CHI PHÍ: toàn bộ nội dung này được gửi kèm MỖI lượt hỏi, nên nó làm tăng token và
/// độ trễ của mọi request chat. Hiện chấp nhận được vì tài liệu còn nhỏ. Khi nội dung
/// lớn hơn nhiều, hướng đúng là tìm kiếm ngữ nghĩa (RAG) để chỉ nạp phần liên quan —
/// repo đã có chỗ dành sẵn ở Infrastructure/ExternalServices/AzureSearch.
/// </summary>
public static class PlatformKnowledge
{
    /// <summary>Quy trình nghiệp vụ đưa vào system instruction.</summary>
    public const string Workflows = """
        ================= KIẾN THỨC VỀ NỀN TẢNG =================
        Phần này để bạn GIẢI THÍCH QUY TRÌNH cho người dùng. Đây là mô tả hệ thống,
        KHÔNG phải dữ liệu của một người dùng cụ thể — đừng trộn số liệu vào đây.

        [A] GIẢI THÍCH CÁC TRẠNG THÁI NGƯỜI DÙNG NHÌN THẤY
        Đơn đặt vẽ (commission): Chờ chấp nhận → Đang thực hiện → Đang thương lượng →
        Đã nộp bản cuối → Hoàn tất / Đã huỷ / Đang tranh chấp.
        Mốc công việc (milestone): Chờ làm → Đang làm → Đã nộp → Đã duyệt → Yêu cầu sửa.
        Ký quỹ (escrow): Chờ nạp → Đã nạp → Giải ngân một phần → Đã giải ngân hết →
        Đã hoàn → Đang tranh chấp → Chia đôi.
        Phiên đấu giá: Đã lên lịch → Đang diễn ra → Đã kết thúc → Đã chốt /
        Đã huỷ / Quá hạn thanh toán.
        Lượt đặt giá: Đang dẫn đầu → Bị đè giá → Thắng / Thua / Đã huỷ / Quá hạn.
        Đơn nạp tiền: Chờ thanh toán → Đã thanh toán → Thất bại / Hết hạn / Đã huỷ.
        Yêu cầu rút tiền: Chờ duyệt → Đã chuyển khoản / Bị từ chối (tiền đã hoàn về ví).
        Tranh: Chờ kiểm duyệt → Đã duyệt / Bị từ chối / Đã ẩn.
        Hồ sơ đăng ký Creator: Chờ duyệt → Đã duyệt / Bị từ chối / Cần bổ sung bằng chứng.

        [B] TÀI KHOẢN
        - Đăng ký bằng email, cần xác thực OTP. Một tài khoản có thể vừa là người mua
          vừa là hoạ sĩ (đổi vai trò trong ứng dụng, không cần tạo tài khoản mới).
        - Quên mật khẩu: dùng chức năng gửi lại link/OTP qua email.
        - Cài đặt hồ sơ: ảnh đại diện, ảnh bìa, tiểu sử, thông tin liên hệ.

        [C] TRỞ THÀNH HOẠ SĨ (CREATOR)
        - Người dùng gửi hồ sơ đăng ký kèm bằng chứng năng lực (portfolio, video
          speedpaint, giấy tờ tuỳ thân).
        - Kiểm duyệt viên duyệt hoặc từ chối; có thể yêu cầu bổ sung bằng chứng.
        - Khi được duyệt, tài khoản nhận thêm vai trò hoạ sĩ và có thể đăng tranh, nhận
          đơn đặt vẽ, tạo phiên đấu giá, tạo mã giảm giá.
        - Một số hồ sơ được cấp huy hiệu "xác thực vẽ tay" nếu chứng minh được tác phẩm
          không do AI tạo.

        [D] ĐĂNG TRANH LÊN HỒ SƠ
        - Tranh mới đăng luôn ở trạng thái CHỜ KIỂM DUYỆT, chưa hiện công khai.
        - Hệ thống tự quét an toàn nội dung và gắn thẻ tự động, đồng thời tạo bản có
          watermark để trưng bày.
        - Người đăng phải khai báo nếu tác phẩm do AI tạo.
        - Kiểm duyệt viên duyệt thì tranh mới hiện công khai.

        [E] VÍ VÀ NẠP TIỀN
        - Ví có hai loại số dư: "số dư khả dụng" (dùng để đặt giá, rút tiền) và "tiền đang
          giữ" (tiền cọc đấu giá hoặc tiền ký quỹ đơn đặt vẽ — không rút được).
        - Nạp tiền: vào mục Ví, nhập số tiền, chọn cổng thanh toán. Hiện nền tảng hỗ trợ
          payOS (chuyển khoản ngân hàng / quét mã QR). Tiền vào ví ngay khi cổng xác nhận.
        - Nếu đã chuyển khoản mà ví chưa cộng, chờ vài phút rồi tải lại trang; nếu vẫn
          chưa có, liên hệ hỗ trợ kèm mã đơn nạp tiền.
        - Ví có thể bị tạm khoá khi đang điều tra; ví bị khoá thì không đặt giá hay rút
          tiền được.

        [F] RÚT TIỀN
        - Cần thêm tài khoản ngân hàng trước (tên ngân hàng, số tài khoản, chủ tài khoản).
          Tài khoản đầu tiên tự động là mặc định; chỉ có một tài khoản mặc định.
        - Số dư rút được là "số dư khả dụng"; tiền đang giữ trong escrow/cọc KHÔNG rút được.
        - Số tiền rút tối thiểu mặc định là 50.000 VND (quản trị viên có thể đổi).
        - Tiền bị trừ khỏi ví NGAY khi gửi yêu cầu, rồi chờ quản trị viên duyệt.
        - Nếu yêu cầu bị từ chối, tiền được hoàn lại ví. Nếu được duyệt, tiền chuyển vào
          tài khoản ngân hàng đã đăng ký.
        - Không xoá được tài khoản ngân hàng đang có yêu cầu rút tiền chờ duyệt.

        [G] ĐẤU GIÁ TRANH
        - Ai đang sở hữu tranh đều tạo được phiên: giá khởi điểm, bước giá, giá mua ngay
          (tuỳ chọn), giá sàn bí mật (tuỳ chọn), thời gian bắt đầu và kết thúc.
        - Giá sàn bí mật: nếu giá cao nhất khi kết thúc chưa đạt mức này, phiên KHÔNG bán
          được và mọi khoản cọc được hoàn. Người ngoài không nhìn thấy giá sàn.
        - Sửa phiên: chỉ khi phiên còn ở trạng thái "đã lên lịch" và CHƯA có ai đặt giá.
        - Huỷ phiên: chỉ khi chưa có ai đặt giá. Phiên đã chốt thì không huỷ được.
        - Người bán không được tự đặt giá cho phiên của mình.
        - Cách đặt giá:
            · Ví phải đủ số dư khả dụng.
            · Lượt đặt giá ĐẦU TIÊN phải bằng hoặc cao hơn giá khởi điểm.
            · Từ lượt THỨ HAI trở đi, phải cao hơn giá hiện tại ít nhất một bước giá.
            · Đặt giá thành công thì hệ thống khoá đúng số tiền đó trong ví.
        - Bị đè giá: tiền cọc được hoàn về ví ngay lập tức và người bị đè nhận thông báo.
        - Mua ngay: nếu phiên có giá mua ngay, người mua trả trực tiếp từ số dư khả dụng,
          phiên kết thúc ngay, và cọc của những người đang dẫn đầu được hoàn.
        - Theo dõi phiên: bật theo dõi để nhận cảnh báo khi bị đè giá. Bỏ theo dõi bất kỳ lúc nào.
        - Chốt phiên: hết thời gian, người trả giá cao nhất thắng (nếu đạt giá sàn). Tiền
          chuyển cho người bán sau khi trừ phí nền tảng; quyền sở hữu tranh chuyển sang
          người thắng; người thắng được cấp link tải file gốc.
        - Hạn thanh toán của người thắng mặc định là 24 giờ sau khi chốt phiên
          (quản trị viên có thể đổi). Quá hạn thì kết quả phiên có thể bị huỷ và người
          trả giá cao tiếp theo được xem xét.
        - Một tranh không thể nằm trong hai phiên cùng lúc.

        [H] TẢI FILE GỐC
        - Chỉ người thắng phiên (đã thanh toán) hoặc người mua hoàn tất đơn đặt vẽ mới tải được.
        - Link tải có thời hạn, mặc định 15 phút (quản trị viên có thể đổi trong khoảng
          5–120 phút). Hết hạn thì tạo lại link mới.
        - Ảnh trưng bày công khai luôn có watermark; file gốc mới là bản không watermark.

        [I] ĐẶT VẼ TRANH (COMMISSION) — QUY TRÌNH ĐẦY ĐỦ
        Thứ tự bắt buộc, không nhảy bước:
        1. Người mua gửi yêu cầu đặt vẽ kèm mô tả, tài liệu tham khảo, thời hạn và các mốc.
           Đơn ở trạng thái "Chờ chấp nhận".
        2. Hoạ sĩ chấp nhận, từ chối, hoặc thương lượng lại giá. Đơn chỉ chuyển sang
           "Đang thực hiện" khi hoạ sĩ đã chấp nhận.
        3. Người mua nạp tiền vào ký quỹ (escrow). Phải chấp nhận đơn TRƯỚC mới nạp được,
           và không nạp lặp hai lần.
        4. Hoạ sĩ nộp sản phẩm của mốc HIỆN TẠI kèm ảnh có watermark để xem trước.
           Không nộp vượt mốc.
        5. Người mua duyệt mốc hoặc yêu cầu sửa. Duyệt mốc thì tiền của mốc đó được giải
           ngân cho hoạ sĩ (mỗi mốc chỉ giải ngân một lần).
        6. Khi TẤT CẢ các mốc đã được duyệt và tiền ký quỹ đã giải ngân hết, hoạ sĩ bàn
           giao bộ file hoàn chỉnh.
        7. Người mua nghiệm thu và tải file gốc. Đơn chuyển sang "Hoàn tất".
        Lưu ý:
        - Số lần yêu cầu sửa miễn phí mặc định là 2 lần cho mỗi mốc (quản trị viên có thể
          đặt 0–10 lần). Vượt hạn mức thì hai bên tự thoả thuận.
        - Nếu người mua không phản hồi, hệ thống có thể tự động duyệt mốc sau một số ngày
          (mặc định 7 ngày, quản trị viên có thể đặt 1–30 ngày).
        - Đơn đã hoàn tất, đã huỷ hoặc đang tranh chấp thì không chuyển tiếp trái quy trình.
        - Hai yêu cầu thay đổi trạng thái gửi cùng lúc có thể bị từ chối (lỗi xung đột),
          chỉ cần thử lại.

        [J] ĐÁNH GIÁ HOẠ SĨ
        - Chỉ đánh giá được khi đơn đã HOÀN TẤT, và mỗi đơn chỉ đánh giá một lần.
        - Thang 1–5 sao kèm nhận xét. Điểm trung bình hiển thị trên hồ sơ hoạ sĩ.
        - Hoạ sĩ có thể đăng phản hồi công khai cho đánh giá đó.

        [K] TRANH CHẤP
        - Người mua hoặc hoạ sĩ mở tranh chấp cho một đơn đặt vẽ khi không thống nhất được,
          kèm mô tả vấn đề và bằng chứng.
        - Kiểm duyệt viên hoặc quản trị viên xem lại lịch sử trao đổi, bằng chứng, rồi quyết
          định tỉ lệ hoàn tiền cho người mua (0%–100%). Phần còn lại trả cho hoạ sĩ, sau khi
          trừ phí nền tảng.
        - Việc chia tiền được thực hiện một lần, nguyên tử (hoặc hoàn hết, hoặc trả hết, hoặc
          chia theo tỉ lệ). Một vụ chỉ phân xử một lần.
        - Không tự xử lý tranh chấp ngoài hệ thống — hãy dùng chức năng "Khiếu nại" trong đơn
          để còn bằng chứng và thời gian phản hồi.

        [L] MÃ GIẢM GIÁ (VOUCHER)
        - Do quản trị viên hoặc hoạ sĩ tạo. Giảm theo phần trăm (có thể chặn mức giảm tối đa)
          hoặc giảm số tiền cố định.
        - Phạm vi áp dụng chọn được: đơn đặt vẽ, phiên đấu giá, hoặc giao dịch nạp tiền —
          một mã có thể áp cho nhiều loại cùng lúc.
        - Mỗi mã có: ngày bắt đầu, ngày kết thúc, giá trị đơn tối thiểu, tổng số lượt dùng.
        - Điều kiện hợp lệ: đang trong thời gian hiệu lực, chưa bị tắt, còn lượt, đơn đạt
          giá trị tối thiểu, đúng phạm vi, và người dùng CHƯA từng dùng mã này.
        - Mỗi người chỉ dùng một mã MỘT lần; mỗi giao dịch chỉ áp được MỘT mã.
        - Nhập mã ở bước thanh toán; hệ thống kiểm tra ngay và nói rõ lý do nếu không dùng được.

        [M] PHÒNG LÀM VIỆC (WORKROOM) VÀ TRÒ CHUYỆN
        - Mỗi đơn đặt vẽ có một phòng riêng cho người mua và hoạ sĩ.
        - Gửi được tin nhắn, ảnh và tệp đính kèm (tối đa 25 MB mỗi tệp; nhận ảnh
          jpg/png/webp/gif, pdf, zip, docx và txt).
        - Có chỉ báo "đang gõ" và trạng thái đã đọc.
        - Tin nhắn dịch được sang tiếng Việt, Anh, Nhật, Hàn, Trung hoặc Pháp. Bản dịch
          được lưu lại nên mở lại không phải dịch lại.

        [N] THÔNG BÁO
        - Người dùng nhận thông báo TRONG ỨNG DỤNG theo thời gian thực (biểu tượng chuông).
        - Các loại thông báo chính: bị đè giá, phiên sắp kết thúc, thắng phiên, cảnh báo rủi
          ro trễ hạn, nạp tiền thành công, yêu cầu rút tiền đổi trạng thái, tài khoản bị xử phạt.
        - Có thể đánh dấu đã đọc từng thông báo hoặc tất cả.
        - Kênh Email và Push đang được hoàn thiện; hiện chỉ kênh trong ứng dụng hoạt động.

        [O] NHẮC NHỞ VÀ CẢNH BÁO TRỄ HẠN
        - Hệ thống tính điểm rủi ro trễ hạn cho mỗi đơn (thang 0–100) dựa trên: thời gian còn
          lại tới hạn, số mốc chưa xong, và số lần phải sửa.
        - Mức rủi ro: thấp (dưới 35), trung bình (35–59), cao (60–79), nghiêm trọng (từ 80).
        - Người dùng đặt được số giờ nhắc trước hạn (1–720 giờ) và bật/tắt kênh nhận nhắc nhở.
        - Cũng có thể tự tạo nhắc nhở thủ công cho một mốc cụ thể.

        [P] DOANH THU CỦA HOẠ SĨ
        - Tổng thu: tiền hoạ sĩ thực nhận từ các giao dịch đã hoàn tất.
        - Phí nền tảng: khoản đã trừ.
        - Thu nhập ròng = tổng thu − phí nền tảng.
        - Số đơn hoàn tất và giá trị trung bình mỗi đơn.
        - Biểu đồ theo ngày / tuần / tháng; phân rã theo nguồn (đơn đặt vẽ hoặc đấu giá).
        - Có bản chốt doanh thu định kỳ (ngày/tuần/tháng) phục vụ đối soát; chỉ quản trị
          viên được tính lại bản chốt.

        [Q] PHÍ VÀ CHÍNH SÁCH NỀN TẢNG
        - Phí nền tảng mặc định 5% trên giá trị giao dịch; quản trị viên đặt được trong
          khoảng 5%–15%.
        - Tiền rút tối thiểu mặc định 50.000 VND.
        - Link tải file gốc mặc định 15 phút.
        - Hạn thanh toán của người thắng đấu giá mặc định 24 giờ.
        - Số lượt sửa miễn phí mỗi mốc mặc định 2 lần.
        Mọi con số trên là MẶC ĐỊNH và quản trị viên có thể thay đổi.

        [R] XỬ LÝ SỰ CỐ THƯỜNG GẶP
        - "Không đặt giá được": kiểm tra (1) số dư khả dụng có đủ không, (2) giá đặt có đạt
          mức tối thiểu không, (3) phiên còn đang diễn ra không, (4) bạn có phải người bán
          của phiên đó không, (5) giá đặt có đạt giá sàn bí mật không.
        - "Tiền của tôi đang bị giữ": đó là tiền cọc của lượt đặt giá đang dẫn đầu, hoặc tiền
          ký quỹ của đơn đặt vẽ. Cọc được hoàn ngay khi bị đè giá; ký quỹ được giải ngân theo
          mốc hoặc hoàn khi đơn huỷ/tranh chấp được phân xử.
        - "Không rút được tiền": kiểm tra (1) đã thêm tài khoản ngân hàng chưa, (2) số tiền
          có đạt mức tối thiểu không, (3) số dư khả dụng có đủ không (tiền đang giữ không
          rút được), (4) ví có đang bị khoá không.
        - "Không tải được file gốc": cần là người thắng phiên đã thanh toán, hoặc đơn đã
          hoàn tất. Link cũng chỉ sống 15 phút — tạo lại link mới.
        - "Mã giảm giá không dùng được": mã hết hạn, chưa tới ngày bắt đầu, đã hết lượt,
          đơn chưa đạt giá trị tối thiểu, sai phạm vi, hoặc bạn đã dùng mã này rồi.
        - "Tranh không hiện công khai": tranh mới luôn chờ kiểm duyệt; hoặc đã bị từ chối/ẩn.
        - "Không nhận được thông báo email": kênh email chưa hoạt động, hiện chỉ có thông
          báo trong ứng dụng.
        - "Hai yêu cầu cùng lúc báo lỗi xung đột": hệ thống chống ghi đè đồng thời; chỉ cần
          gửi lại một lần.
        =========================================================
        """;
}
