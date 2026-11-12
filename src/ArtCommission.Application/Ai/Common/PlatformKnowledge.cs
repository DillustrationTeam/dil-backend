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
///      có thể đổi" vì phần lớn đọc từ PlatformConfig. Con số đang đặt trong DB có thể
///      khác mặc định — khi đó bot phải nói "xem con số thật ở màn hình", không khẳng định.
///   2. KHÔNG mô tả tính năng chưa có (ví dụ: kênh Email/Push chưa có worker gửi; giá sàn
///      bí mật đã bị bỏ; không còn hạn thanh toán 24 giờ vì tiền bị khoá ngay khi đặt giá).
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
        Chờ người mua chọn hình thức nhận → Chờ người bán gửi hàng → Đang vận chuyển →
        Hoàn tất / Đang khiếu nại / Đã huỷ.
        Lượt đặt giá: Đang dẫn đầu → Bị đè giá → Thắng / Thua / Đã huỷ.
        Đơn giao tranh vật lý: Chờ người bán gửi hàng → Đang vận chuyển → Đã giao →
        Đang khiếu nại → Đã xử lý / Đã huỷ.
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
        - Số tiền rút tối thiểu mặc định là 2.000 VND (quản trị viên có thể đổi).
        - Tiền bị trừ khỏi ví NGAY khi gửi yêu cầu, rồi chờ quản trị viên duyệt.
        - Nếu yêu cầu bị từ chối, tiền được hoàn lại ví. Nếu được duyệt, tiền chuyển vào
          tài khoản ngân hàng đã đăng ký.
        - Không xoá được tài khoản ngân hàng đang có yêu cầu rút tiền chờ duyệt.

        [G] ĐẤU GIÁ TRANH — TOÀN BỘ QUY TRÌNH (phần quan trọng nhất, đọc kỹ)
        G1. Tạo phiên
        - Người đang SỞ HỮU tranh tạo được phiên (kể cả tranh mua lại từ phiên trước).
        - Khai báo: giá khởi điểm, bước giá, giá mua ngay (không bắt buộc), thời gian bắt
          đầu và kết thúc.
        - Một tranh không thể nằm trong hai phiên cùng lúc.
        - Chỉ SỬA được phiên khi chưa có ai đặt giá. Chỉ HUỶ được phiên khi chưa có ai
          đặt giá; phiên đã chốt thì không huỷ.
        - Người bán đang bị tạm khoá bán (do vi phạm giao hàng) thì chưa tạo được phiên
          mới cho tới khi hết hạn khoá.

        G2. Đặt giá
        - Ví phải đủ số dư khả dụng; hệ thống khoá đúng số tiền của lượt đặt giá đang
          dẫn đầu, nên "tiền đang giữ" tăng lên và không rút được.
        - Lượt đặt giá ĐẦU TIÊN phải bằng hoặc cao hơn giá khởi điểm.
        - Từ lượt THỨ HAI, phải cao hơn giá hiện tại ít nhất một bước giá.
        - Người bán không được tự đặt giá cho phiên của mình.
        - Bị đè giá: tiền khoá được trả lại ví NGAY, kèm thông báo.
        - Đặt giá tự động (auto-bid): người dùng đặt một "giá trần" bí mật, hệ thống tự
          nâng giá thay họ theo từng bước giá cho tới khi chạm giá trần. Giá trần không
          hiển thị cho người khác. Nếu hai người cùng đặt giá trần, ai đăng ký trước thì
          thắng khi cùng mức.
        - Nếu phiên vừa được người khác đặt giá, lượt đặt của bạn có thể bị từ chối kèm
          giá tối thiểu mới — chỉ cần xem lại giá rồi đặt lại.

        G3. Mua ngay
        - Phiên có giá mua ngay: trả đúng mức đó là thắng ngay, phiên kết thúc lập tức.
        - Giá mua ngay là mức TRẦN CỨNG: nếu một lượt đặt giá chạm hoặc vượt giá mua
          ngay, hệ thống chốt luôn phiên ở đúng giá mua ngay cho người vừa đặt.
        - Mua ngay xong thì tiền cọc của người đang dẫn đầu trước đó được trả lại ví.

        G4. Chốt phiên và tiền
        - Hết thời gian, người trả giá cao nhất thắng; tiền lấy từ khoản đã khoá của
          người thắng.
        - Phí nền tảng trừ vào phần người BÁN nhận, không cộng thêm cho người mua.
          Phí hiện đặt 10%; quản trị viên chỉnh được trong khoảng 5%–15%.
          Ví dụ: chốt 1.000.000 VND, phí 10% = 100.000 VND → người bán nhận 900.000 VND.
        - Người bán đang bị phạt giao hàng thì bị cộng thêm phụ phí: mức 3 là +20 điểm
          phần trăm (tổng 30%), mức 4 là +40 điểm phần trăm (tổng 50%). Mức phụ phí được
          chốt tại thời điểm phiên kết thúc; đổi cấu hình sau đó không làm thay đổi phiên
          đã chốt.
        - Quyền sở hữu tranh chuyển cho người thắng. Người thắng xem được chứng nhận sở
          hữu, các biên nhận và (nếu có) văn bản quyền thương mại, và tải được file gốc.
        - Người thắng đánh giá người bán 1–5 sao, mỗi phiên một lần, sau khi giao dịch
          khép lại.

        G5. Tranh có bản VẬT LÝ — người mua có 48 giờ để chọn
        - Sau khi chốt, tiền được GIỮ ở sàn và phiên chuyển sang "Chờ người mua chọn hình
          thức nhận". Người mua có 48 giờ chọn một trong hai:
          (a) Yêu cầu gửi tranh thật: điền tên người nhận, số điện thoại, địa chỉ →
              đơn chuyển "Chờ người bán gửi hàng".
          (b) Chỉ nhận file số: đơn kết thúc ngay, tiền chuyển cho người bán sau khi trừ phí.
        - Quá 48 giờ mà không chọn: phiên TỰ HUỶ, toàn bộ tiền hoàn về ví người mua, tranh
          trở lại quyền sở hữu người bán, người mua không nhận được file gốc của phiên đó.
          Trường hợp này KHÔNG tính là lỗi người bán nên người bán KHÔNG bị phạt.
        - Người bán gửi hàng: nhập hãng vận chuyển + mã vận đơn, kèm ÍT NHẤT MỘT bằng
          chứng (link tra cứu hành trình, ảnh biên lai bưu cục, hoặc ảnh kiện hàng).
          Thiếu bằng chứng thì hệ thống không nhận.
        - Người mua bấm "Đã nhận hàng": tiền chuyển cho người bán, phiên hoàn tất.
        - Người mua không bấm gì: sau 48 giờ kể từ lúc người bán gửi hàng, hệ thống TỰ
          ĐỘNG xác nhận đã nhận và chuyển tiền cho người bán.

        G6. Khiếu nại giao tranh vật lý và thang phạt người bán
        - Người mua gửi khiếu nại kèm mô tả và ảnh bằng chứng khi đơn còn mở. Tiền đang
          giữ bị ĐÓNG BĂNG, không chuyển cho ai.
        - Quản trị viên xem bằng chứng hai bên rồi quyết định: hoàn tiền cho người mua,
          hoặc trả tiền cho người bán.
        - Nếu tiền đã chuyển cho người bán trước khi khiếu nại (đã giải ngân) thì không
          hoàn lại được nữa, nhưng khiếu nại vẫn được ghi nhận để xử lý và vẫn có thể
          tính mức phạt cho người bán.
        - Thang phạt người bán — CHỈ TĂNG, KHÔNG GIẢM:
          · Mức 1: nhắc nhở, không mất tiền, vẫn bán bình thường.
          · Mức 2: nhắc nhở + không được đăng bán 1 ngày.
          · Mức 3: 5 phiên bán tiếp theo bị trừ thêm 20% phí — bán 1.000.000 VND nhận
            700.000 VND thay vì 900.000 VND.
          · Mức 4: 5 phiên bán tiếp theo bị trừ thêm 40% phí — bán 1.000.000 VND nhận
            500.000 VND — và không được đăng bán 7 ngày.
          · Mức 5: không được đăng bán tranh nữa (vĩnh viễn).
        - Đường vào thang phạt: nếu TIỀN CÒN Ở SÀN khi bị xác nhận thì mỗi lần bị xác
          nhận lên 1 mức. Nếu TIỀN ĐÃ VỀ VÍ NGƯỜI BÁN thì mỗi lần bị xác nhận lên 2 mức
          (lần đầu tiên đã vào thẳng mức 3).
        - Ba con số của thang phạt (20 điểm phần trăm phụ phí, 5 phiên, 7 ngày) là mặc
          định, quản trị viên đổi được.

        G7. Không đặt giá / không mua được — kiểm tra theo thứ tự
        1. Số dư khả dụng của ví có đủ không.
        2. Giá đặt đã đạt mức tối thiểu chưa (giá khởi điểm, hoặc giá hiện tại + bước giá).
        3. Phiên còn đang diễn ra không (hết giờ là không đặt được nữa).
        4. Bạn có phải người bán của phiên đó không (người bán không được tự đặt giá).
        5. Phiên đã chốt chưa (đã chạm giá mua ngay hoặc đã hết giờ thì không đặt tiếp).

        G8. Chứng từ của phiên đấu giá
        - Biên nhận đặt cọc (tiền giữ khi đặt giá), biên nhận gửi bưu cục, biên nhận giao
          hàng, biên nhận chuyển tiền cho người bán.
        - Chỉ người thắng, người bán và quản trị viên xem được các chứng từ này.

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
        - Chỉ đánh giá được khi đơn đã HOÀN TẤT (hoặc phiên đấu giá đã khép lại), và mỗi
          đơn/phiên chỉ đánh giá một lần.
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
        - Với phiên đấu giá có bản vật lý, xem mục G6.
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
        - Phí nền tảng: cấu hình được trong khoảng 5%–15%; mức đang đặt hiện tại là 10%,
          trừ vào tiền NGƯỜI BÁN nhận. Con số chính xác của từng phiên hiển thị ở phần
          thanh toán/chốt phiên của phiên đó — hãy chỉ người dùng xem ở đó thay vì đoán.
        - Người bán đang bị phạt giao hàng: cộng thêm 20 hoặc 40 điểm phần trăm phí (mục G6).
        - Tiền rút tối thiểu mặc định 2.000 VND (quản trị viên có thể đổi); rút tiền không
          mất phí.
        - Link tải file gốc mặc định 15 phút.
        - Số lượt sửa miễn phí mỗi mốc mặc định 2 lần.
        - Người mua có 48 giờ để chọn hình thức nhận tranh vật lý, và 48 giờ để xác nhận
          đã nhận hàng sau khi người bán gửi.
        Mọi con số trên là MẶC ĐỊNH và quản trị viên có thể thay đổi.

        [R] XỬ LÝ SỰ CỐ THƯỜNG GẶP
        - "Không đặt giá được": kiểm tra (1) số dư khả dụng có đủ không, (2) giá đặt có đạt
          mức tối thiểu không, (3) phiên còn đang diễn ra không, (4) bạn có phải người bán
          của phiên đó không, (5) phiên đã chốt chưa.
        - "Tiền của tôi đang bị giữ": đó là tiền cọc của lượt đặt giá đang dẫn đầu, hoặc tiền
          ký quỹ của đơn đặt vẽ/phiên đang chờ giao tranh. Cọc được hoàn ngay khi bị đè giá
          hoặc khi phiên kết thúc mà bạn không thắng; tiền của phiên có tranh vật lý chỉ
          chuyển cho người bán sau khi giao xong (hoặc sau 48 giờ tự động xác nhận).
        - "Phiên tự huỷ, tôi không nhận được tranh": người thắng không chọn hình thức nhận
          trong 48 giờ nên hệ thống huỷ phiên và hoàn tiền (mục G5). Người bán không bị phạt.
        - "Người bán không gửi hàng": dùng chức năng "Khiếu nại" trong phiên kèm bằng chứng;
          tiền vẫn đang giữ ở sàn nên còn xử lý được.
        - "Tôi bị phạt mức 3/4 và thấy phí cao hơn": xem mục G6 — phụ phí được cộng vào phí
          của các phiên bán tiếp theo, không phải phí của phiên đã chốt trước đó.
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
