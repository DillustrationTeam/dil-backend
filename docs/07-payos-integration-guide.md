# Hướng dẫn tích hợp payOS & gán Webhook

> 📁 **Quy ước đường dẫn trong tài liệu này:**
>
> - `<repo>` = thư mục gốc repo `dil-backend` **trên máy bạn** (chỗ chứa file `ArtCommission.sln`). Mỗi máy một khác.
> - Các lệnh ghi `cd src/ArtCommission.API` nghĩa là chạy từ `<repo>`.
> - Đường dẫn trong repo (như `docs/07-payos-integration-guide.md`) là **đường dẫn tương đối từ `<repo>`**, không phải đường dẫn tuyệt đối.

---

## 0. TL;DR — làm nhanh nhất

Mở PowerShell, `cd` vào thư mục gốc repo `dil-backend` (chỗ có `ArtCommission.sln`) trước.

```powershell
# 1. Lấy 3 key payOS (xem mục 2), lưu vào .env ở thư mục gốc workspace
# 2. Nạp key vào project
cd src/ArtCommission.API
dotnet user-secrets init
dotnet user-secrets set "PayOS:ClientId"    "<Client ID>"
dotnet user-secrets set "PayOS:ApiKey"      "<API Key>"
dotnet user-secrets set "PayOS:ChecksumKey" "<Checksum Key>"

# 3. Chạy API (giữ cửa sổ này mở)
dotnet run --launch-profile http          # http://localhost:5075

# 4. Mở tunnel — mở CỬA SỔ POWERSHELL KHÁC, cd về thư mục gốc repo
ngrok http 5075

# 5. Gán webhook URL vào payOS (mục 5)
#    https://<url-ngrok>/api/v1/payments/webhooks/payos

# 6. Test KHÔNG cần tiền thật
#    Mở Swagger http://localhost:5075/swagger → tự sinh webhook + chữ ký (mục 6b)
```

---

## 1. payOS là gì và khác gì các cổng khác

| Điểm             | Chi tiết                                                                                                    |
| ------------------ | ------------------------------------------------------------------------------------------------------------ |
| Loại              | Cổng thanh toán VN — tạo link/QR VietQR để khách chuyển khoản                                       |
| Tiền tệ          | **Chỉ VND**                                                                                           |
| Webhook            | payOS gọi POST về server mình khi khách trả tiền                                                       |
| Xác thực webhook | HMAC-SHA256 với`ChecksumKey`                                                                              |
| **Sandbox**  | ⚠️**KHÔNG CÓ.** Không có môi trường test riêng — test là chạy trên môi trường thật   |
| Đăng ký         | Cần**CCCD** (cá nhân) hoặc **MST** (doanh nghiệp) + **tài khoản ngân hàng thật** |

Nguồn chính thức: [.NET SDK](https://payos.vn/docs/sdks/back-end/net/) · [Webhook](https://payos.vn/docs/du-lieu-tra-ve/webhook/) · [Kiểm tra signature](https://payos.vn/docs/tich-hop-webhook/kiem-tra-du-lieu-voi-signature) · [Môi trường test](https://payos.vn/docs/moi-truong-test/)

---

## 2. Lấy 3 key payOS

### Bước 2.1 — Tạo tài khoản

1. Vào https://my.payos.vn/login → **Đăng ký**
2. Điền thông tin → **Tạo tài khoản**
3. Mở email → bấm **Xác thực**

### Bước 2.2 — Xác thực tổ chức ⚠️ (chỗ dễ tắc nhất)

payOS gọi mọi khách hàng là "Tổ chức", chia 2 loại. **Chọn đúng loại**:

| Loại                                            | Chuẩn bị                         |
| ------------------------------------------------ | ---------------------------------- |
| **Tài khoản cá nhân / Hộ kinh doanh** | Số**CCCD/CMND** + họ tên  |
| **Doanh nghiệp (có pháp nhân)**        | **Mã số thuế** hoặc GPKD |

1. Chọn loại → điền CCCD/MST → **Kiểm tra**
2. Hiện **Thành công** + tự điền tên → **Tiếp tục**
3. **Chuyển khoản xác thực** bằng cách quét QR

**3 điều kiện bắt buộc của bước chuyển khoản:**

- Chuyển từ ngân hàng **trùng tên** với tổ chức đã khai
- **KHÔNG** chuyển từ ví điện tử (MoMo, ZaloPay…)
- Đúng **nội dung** và đúng **số tiền**

**Nếu báo không tìm thấy CCCD/MST** → xác thực thủ công: gửi ảnh **2 mặt CCCD** (doanh nghiệp gửi thêm GPKD có mộc đỏ) tới email hỗ trợ payOS, tiêu đề `[payOS] Xác thực thủ công + email dùng payOS`.

### Bước 2.3 — Liên kết tài khoản ngân hàng

payOS dùng **Cas** (sản phẩm của Casso) để kết nối ngân hàng.

1. Menu **Ngân hàng** → chọn ngân hàng → **Tiếp tục**
2. Chọn 1 trong 2 cách:
   - Quét QR bằng **app Cas**, hoặc
   - **"Kết nối trước – Cas sau"** → nhập tay: **CCCD, SĐT, STK**

⚠️ Chỉ thêm được tài khoản ngân hàng **trùng tên** với tổ chức đã xác thực ở bước 2.2.

### Bước 2.4 — Tạo kênh thanh toán → LẤY KEY

Điều kiện: đã xong bước 2.2 **và** 2.3.

1. Menu **Kênh thanh toán** → **Tạo kênh thanh toán**
2. Điền **Tên kênh** + tải **Logo** → **Tiếp tục**
3. Chọn **ngân hàng chính** → **Tạo kênh thanh toán và tích hợp**
4. Màn hình thành công hiện **3 key**:

| Key              | Dùng để làm gì                                                                            |
| ---------------- | ---------------------------------------------------------------------------------------------- |
| `Client ID`    | Khởi tạo`PayOSClient`                                                                      |
| `API Key`      | Khởi tạo`PayOSClient`                                                                      |
| `Checksum Key` | **Verify chữ ký webhook** — thiếu cái này là không xác thực được tiền vào |

Bấm **Hoàn tất**.

---

## 3. Lưu key — KHÔNG được commit

⚠️ **3 key này là key PRODUCTION, gắn tiền thật.** Ai có `ChecksumKey` là verify được webhook; ai có `ApiKey` là tạo được link thanh toán.

### Cách chuẩn của dự án (dùng cái này)

Từ thư mục gốc repo `dil-backend`:

```powershell
cd src/ArtCommission.API
dotnet user-secrets init
dotnet user-secrets set "PayOS:ClientId"    "<Client ID>"
dotnet user-secrets set "PayOS:ApiKey"      "<API Key>"
dotnet user-secrets set "PayOS:ChecksumKey" "<Checksum Key>"

# Kiểm tra đã lưu đúng
dotnet user-secrets list
```

User Secrets lưu **ngoài thư mục repo** (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`) → **không thể commit nhầm**.

### Không nên làm

| Cách                               | Vì sao không                                                                                   |
| ----------------------------------- | ------------------------------------------------------------------------------------------------ |
| Ghi thẳng vào`appsettings.json` | File này**được commit** → lộ key cho cả thế giới                                  |
| Dán key vào chat / issue / PR     | Ai đọc được log là có key                                                                 |
| Tạo file`.env` trong repo        | Chỉ an toàn nếu`.gitignore` đã chặn (repo đã chặn `*.env`, nhưng vẫn nên tránh) |

### Khi deploy (không phải local)

Dùng **biến môi trường** (dấu `__` thay dấu `:`):

```
PayOS__ClientId=<...>
PayOS__ApiKey=<...>
PayOS__ChecksumKey=<...>
```

---

## 4. Chạy API

Từ thư mục gốc repo `dil-backend`:

```powershell
cd src/ArtCommission.API
dotnet run --launch-profile http
```

→ API ở `http://localhost:5075` · Swagger ở `http://localhost:5075/swagger`

**Kiểm tra key đã nhận chưa:** xem log khởi động. Nếu thấy dòng này thì **key chưa được nạp**:

```
warn: payOS chưa được cấu hình đủ (PayOS:ClientId / PayOS:ApiKey / PayOS:ChecksumKey).
      Các endpoint thanh toán sẽ báo lỗi cho tới khi cấu hình xong.
```

⚠️ **Khi cần build lại, phải dừng API trước** — nếu không sẽ gặp:

```
error MSB3021: The process cannot access the file ... because it is being used by another process.
      The file is locked by: "ArtCommission.API"
```

Đây **không phải lỗi code**, chỉ là file DLL đang bị khoá. Dừng API rồi build lại là xong.

**Tránh hẳn vấn đề này** — dùng `dotnet watch` (tự dừng, build lại, chạy lại khi sửa code):

```powershell
dotnet watch run --launch-profile http
```

---

## 5. Gán Webhook URL ⚠️ (phần quan trọng nhất)

### Vì sao cần webhook

Khách chuyển khoản → tiền vào tài khoản payOS. **Nhưng backend không tự biết.** payOS phải **gọi POST về server mình** để báo "đơn này đã trả tiền". Không gán webhook thì **ví trên sàn không bao giờ được cộng tiền**.

*(Có fallback: job đối soát tự hỏi payOS mỗi 5 phút. Nhưng chậm — webhook là đường chính.)*

### Vấn đề: webhook cần HTTPS public

payOS là server trên internet, **không gọi được `localhost`**. Khi dev ở máy local phải mở **tunnel** để có URL HTTPS công khai trỏ về máy mình.

### Bước 5.1 — Cài ngrok

```powershell
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe" install Ngrok.Ngrok --accept-package-agreements --accept-source-agreements
```

⚠️ **winget cài bản 3.3.1 — QUÁ CŨ, sẽ bị payOS/tài khoản ngrok từ chối với lỗi `ERR_NGROK_121`.** Phải cập nhật:

```powershell
# Đường dẫn đầy đủ vì PATH có thể chưa nạp trong cửa sổ đang mở
& "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Ngrok.Ngrok_Microsoft.Winget.Source_8wekyb3d8bbwe\ngrok.exe" update
```

Sau khi update sẽ lên bản mới (3.39+). Kiểm tra: `ngrok --version`

⚠️ **Nếu sau này gỡ rồi cài lại bằng winget, nó lại về 3.3.1** → phải chạy `ngrok update` lại.

**Cách khác nếu winget lỗi:** tải trực tiếp https://ngrok.com/download → giải nén → thêm thư mục vào PATH.

### Bước 5.2 — Lấy authtoken và gắn

1. Đăng ký https://dashboard.ngrok.com/signup (dùng được Google/GitHub)
2. Lấy token ở https://dashboard.ngrok.com/get-started/your-authtoken
3. Gắn token:

```powershell
ngrok config add-authtoken <TOKEN>
```

⚠️ **`ngrok config check` báo OK kể cả khi token chưa hợp lệ.** Muốn chắc thì phải chạy tunnel thật (bước sau) rồi xem có lỗi authen không.

⚠️ Token ngrok là **secret** — đừng dán vào chat, đừng commit.

### Bước 5.3 — Chạy tunnel

```powershell
ngrok http 5075
```

Kết quả:

```
Forwarding   https://abc123-xyz.ngrok-free.app -> http://localhost:5075
```

**Copy URL đó.** Webhook URL của bạn là:

```
https://abc123-xyz.ngrok-free.app/api/v1/payments/webhooks/payos
```

⚠️ **Bản free đổi URL mỗi lần restart ngrok** → restart là phải vào payOS gán lại webhook URL.
⚠️ **Đừng đóng cửa sổ ngrok** trong lúc test.

### Bước 5.4 — Xác nhận tunnel đã tới đúng API

Mở URL webhook bằng trình duyệt hoặc chạy:

```powershell
curl.exe -s --ssl-no-revoke -H "ngrok-skip-browser-warning: true" https://<url-ngrok>/api/v1/payments/webhooks/payos
```

Phải thấy:

```json
{"endpoint":"payments.webhook","gateway":"payos","alive":true,"expects":"POST","hint":"..."}
```

⚠️ **Nếu thấy HTML có chữ `ngrok`** → đó là **trang cảnh báo của ngrok free**, chưa tới API. Thêm header `ngrok-skip-browser-warning: true`.

⚠️ **Nếu `curl.exe` báo `schannel: server closed abruptly`** → lỗi của curl trên Windows, **không phải lỗi API**. Ngrok yêu cầu TLS renegotiation, mà schannel của Windows đã chặn tính năng này. Thêm `--ssl-no-revoke`, hoặc dùng `Invoke-RestMethod`:

```powershell
Invoke-RestMethod "https://<url-ngrok>/api/v1/payments/webhooks/payos" -Headers @{ "ngrok-skip-browser-warning" = "true" }
```

⚠️ **Nếu thấy `HTTP ERROR 405`** khi mở bằng trình duyệt → **bình thường**. Endpoint webhook chỉ nhận **POST** (payOS luôn POST). GET vào đó sẽ 405, trừ khi API có endpoint health GET.

### Bước 5.5 — Gán webhook vào payOS

**Cách A — qua giao diện (khuyến nghị):**

1. Vào https://my.payos.vn → **Kênh thanh toán**
2. Chọn kênh thanh toán của bạn
3. Thêm **webhook URL**: `https://<url-ngrok>/api/v1/payments/webhooks/payos`
4. Lưu

payOS sẽ **tự test endpoint** trước khi nhận — nên **API phải đang chạy** khi làm bước này.

**Cách B — qua API (khi cần tự động hoá):**

```csharp
var result = await payOSClient.Webhooks.ConfirmAsync(webhookUrl);
```

---

## 6. Test module thanh toán

### ⚠️ Không bắt buộc có ngrok để test

Ngrok chỉ cần cho **giao dịch thật** (để payOS gọi webhook về). Muốn test nghiệp vụ tiền thì **không cần** — bạn tự gửi webhook với chữ ký do mình sinh ra.

### Cách 1 — Test bằng Swagger, không cần tiền thật ⭐

Đây là cách nhanh nhất, không cần viết script.

1. Mở `http://localhost:5075/swagger`
2. Gọi `POST /api/v1/auth/register` → lấy `accessToken`
3. Bấm **Authorize** → dán token (⚠️ token sống **15 phút**)
4. Gọi `POST /api/v1/payments/orders` với `{ "amount": 2000, "gateway": "PayOS" }`
   → nhận `paymentUrl`, `qrCode`, và `paymentOrderId`
5. Gọi `GET /api/v1/payments/orders/{paymentOrderId}` → xem `gatewayOrderCode` (cần cho bước 6)
6. Gọi `POST /api/v1/payments/webhooks/payos` với body tự sinh (xem mục 6b)
7. Gọi lại `GET /api/v1/payments/orders/{paymentOrderId}` → `walletBalance` phải tăng

### 6b. Tự sinh chữ ký webhook

payOS ký webhook bằng **HMAC-SHA256** trên object `data`. Muốn tự test, phải sinh chữ ký **giống hệt** thuật toán của payOS.

**Thuật toán (theo đúng source `payOS.Crypto.CryptoProvider`):**

1. Lấy object `data` (KHÔNG phải cả payload)
2. **Làm phẳng** mọi giá trị thành chuỗi; `null` → chuỗi rỗng `""`; `true`/`false` → `"true"`/`"false"`
3. Sắp xếp key theo **ORDINAL** (so từng ký tự theo mã Unicode) — ⚠️ **không phải theo culture**
4. Nối thành chuỗi `key1=value1&key2=value2...` (**không** URL-encode)
5. `HMAC-SHA256(checksumKey, chuỗi đó)` → hex **chữ thường**

**⚠️ 3 cái bẫy đã gặp thật:**

| Bẫy                                   | Hậu quả                                                                                                        |
| -------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Sort key theo culture thay vì ordinal | `currency` đứng trước `counterAccountBankId`, còn ordinal thì ngược lại → chữ ký sai hoàn toàn |
| Không làm phẳng giá trị           | `amount=2000` phải là chuỗi, không phải số                                                               |
| `null` xử lý thành `"null"`     | Phải thành chuỗi rỗng`""`                                                                                  |

**Cách chắc ăn nhất — dùng chính SDK payOS để ký, không tự viết:**

```csharp
// Dùng CryptoProvider của SDK, đảm bảo khớp 100% với lúc verify
var checksumKey = configuration["PayOS:ChecksumKey"];
var client = new PayOSClient("dummy", "dummy", checksumKey);
var signature = client.Crypto.CreateSignatureFromObject(dataObject, checksumKey);
```

Ý tưởng: viết 1 console app nhỏ nhận JSON `data`, gọi `CreateSignatureFromObject`, in ra chữ ký. Rồi dán chữ ký đó vào body webhook trên Swagger. Vì cùng một hàm SDK dùng để verify, chữ ký **luôn khớp**.

**Body webhook mẫu:**

```json
{
  "code": "00",
  "desc": "success",
  "success": true,
  "data": {
    "orderCode": <gatewayOrderCode>,
    "amount": 2000,
    "description": "DIL TEST",
    "accountNumber": "12345678",
    "reference": "FT123456",
    "transactionDateTime": "2026-09-15 13:00:00",
    "currency": "VND",
    "paymentLinkId": "link999",
    "code": "00",
    "desc": "Thanh cong",
    "counterAccountBankId": "",
    "counterAccountBankName": "",
    "counterAccountName": "",
    "counterAccountNumber": "",
    "virtualAccountName": "",
    "virtualAccountNumber": ""
  },
  "signature": "<chữ ký vừa sinh>"
}
```

**Các case nên kiểm:**

| Case                              | Kết quả mong đợi                                          |
| --------------------------------- | ------------------------------------------------------------- |
| Chữ ký sai (`"deadbeef"`)     | `400`, ví **không** đổi                           |
| Sửa`amount` sau khi đã ký   | `400`, ví **không** đổi                           |
| Chữ ký đúng                   | `200`, `applied: true`, ví tăng đúng số tiền        |
| **Gửi lại y hệt lần 2** | `200`, `applied: false`, ví **không** tăng thêm |
| Số tiền lệch số tiền đặt   | `200`, `applied: false`, đơn chuyển `Failed`         |

### Cách 2 — Test bằng công cụ gọi API

Dùng Postman/Insomnia/curl đều được — bản chất giống Cách 1, chỉ khác công cụ.

### Cách 3 — Test end-to-end thật (1 lần cho chắc)

⚠️ **Tốn tiền thật.** Chỉ làm 1 lần với số tiền nhỏ.

1. Tạo đơn nạp 2.000đ → lấy `paymentUrl`
2. Mở link đó → chuyển khoản
3. Kiểm tra ví:

```powershell
$t = (Invoke-RestMethod "http://localhost:5075/api/v1/auth/login" -Method Post -ContentType "application/json" `
      -Body '{"email":"<email>","password":"<pass>"}').data.tokens.accessToken

Invoke-RestMethod "http://localhost:5075/api/v1/payments/orders/<paymentOrderId>" `
  -Headers @{Authorization="Bearer $t"} | Select-Object -ExpandProperty data
```

Kết quả mong đợi:

```
walletBalance      : 2000
paymentOrderStatus : Paid
paidAt             : 2026-09-15T...
```

**Mẹo:** tạo đơn với `clientOrderRef` do bạn tự đặt để dễ tra:

```json
{ "amount": 2000, "gateway": "PayOS", "clientOrderRef": "TEST001" }
```

Rồi lọc: `GET /api/v1/payments/orders?orderRef=TEST001`

---

## 7. Sự cố thường gặp

| Triệu chứng                                      | Nguyên nhân                                                | Cách xử                                                                            |
| -------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------ |
| `payOS chưa được cấu hình đủ` trong log  | Chưa nạp key                                               | Chạy lại`dotnet user-secrets set ...`                                            |
| `ERR_NGROK_121: agent version too old`           | winget cài 3.3.1                                            | `ngrok update`                                                                     |
| `schannel: server closed abruptly`               | curl.exe + TLS renegotiation,**không phải lỗi API** | Thêm`--ssl-no-revoke` hoặc dùng `Invoke-RestMethod`                           |
| `HTTP ERROR 405` mở webhook bằng trình duyệt | Endpoint chỉ nhận POST                                     | Bình thường, không phải lỗi                                                    |
| Thấy HTML chữ`ngrok` thay vì JSON             | Trang cảnh báo ngrok free                                  | Thêm header`ngrok-skip-browser-warning: true`                                     |
| `MSB3021: file is locked by ArtCommission.API`   | API đang chạy                                              | Dừng API rồi build, hoặc dùng`dotnet watch run`                                |
| Webhook gọi về nhưng trả 400                   | Chữ ký sai                                                 | Kiểm`ChecksumKey` đúng chưa; xem mục 6b để biết cách sinh chữ ký đúng |
| Tiền vào payOS nhưng ví không cộng           | Chưa gán webhook URL                                       | Làm mục 5.5. Hoặc chờ job đối soát (5 phút)                                  |
| Webhook ngừng hoạt động sau khi restart máy   | URL ngrok free đổi                                         | Gán lại webhook URL mới vào payOS                                                |

---

## 8. Việc KHÔNG được làm

1. **Không commit key payOS** vào bất kỳ file nào trong repo.
2. **Không dán key/token vào chat, PR, issue** — log và lịch sử đều lưu lại.
3. **Không test bằng tiền lớn.** Test tối đa vài nghìn đồng.
4. **Không xoá `.gitignore`** ở workspace root — nó đang chặn `.env` rò rỉ.
5. **Không đổi `PayOS:BaseUrl`** sang môi trường lạ — payOS không có sandbox, đổi là hỏng.

---

## 9. Checklist lần đầu setup

- [ ] Tài khoản payOS đã xác thực email
- [ ] Tổ chức đã **xác thực thành công** (CCCD hoặc MST)
- [ ] Đã liên kết ≥ 1 tài khoản ngân hàng
- [ ] Đã tạo kênh thanh toán, **có đủ 3 key**
- [ ] Đã nạp 3 key bằng `dotnet user-secrets`
- [ ] `dotnet run --launch-profile http` chạy, Swagger mở được
- [ ] Đã cài ngrok **và chạy `ngrok update`**
- [ ] `ngrok http 5075` chạy, có URL HTTPS
- [ ] Mở URL webhook thấy JSON `"alive":true`
- [ ] Đã gán webhook URL vào payOS
- [ ] Test được webhook bằng chữ ký tự sinh (mục 6b) → ví cộng tiền, gửi lại lần 2 không cộng thêm

---

## 10. Tham chiếu nhanh

| Thứ                                | Ở đâu                                                          |
| ----------------------------------- | ----------------------------------------------------------------- |
| Dashboard payOS                     | https://my.payos.vn                                               |
| Dashboard ngrok                     | https://dashboard.ngrok.com                                       |
| Swagger local                       | http://localhost:5075/swagger                                     |
| Tài liệu payOS chính thức       | https://payos.vn/docs/                                            |
| SDK .NET payOS                      | https://github.com/payOSHQ/payos-lib-dotnet                       |
| Hướng dẫn tích hợp (file này) | `docs/07-payos-integration-guide.md` trong repo `dil-backend` |
| Kiến trúc & convention backend    | `docs/01-architecture.md`, `docs/04-api-conventions.md`       |
| Luật làm việc nhóm (workspace)  | `.agents/` — đọc `00-README.md` trước                    |

---

## 11. Endpoint thanh toán hiện có

| Method                              | Path                                         | Ghi chú                                                   |
| :---------------------------------- | :------------------------------------------- | :--------------------------------------------------------- |
| `POST`                            | `/api/v1/payments/orders`                  | Tạo đơn nạp + link payOS                               |
| `GET`                             | `/api/v1/payments/orders`                  | Lịch sử, cursor, lọc`status`/`gateway`/`orderRef` |
| `GET`                             | `/api/v1/payments/orders/{paymentOrderId}` | Poll trạng thái + tự đối chiếu cổng                 |
| `POST`                            | `/api/v1/payments/webhooks/{gateway}`      | Webhook — verify HMAC-SHA256                              |
| `GET`                             | `/api/v1/payments/webhooks/{gateway}`      | Kiểm tra endpoint còn sống (chẩn đoán)               |
| `GET`/`POST`/`PUT`/`DELETE` | `/api/v1/bank-accounts`                    | Tài khoản ngân hàng nhận tiền                        |
| `POST`/`GET`                    | `/api/v1/payout-requests`                  | Yêu cầu rút tiền                                       |
| `PUT`                             | `/api/v1/payout-requests/{id}/status`      | Admin duyệt/từ chối (role`Administrator`)             |
