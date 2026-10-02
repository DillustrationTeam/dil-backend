using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.ExternalServices.Cloudinary;

public interface ICloudinarySignatureService
{
    /// <summary>
    /// Sinh chữ ký cho Cloudinary Signed Upload — client (browser) dùng chữ ký này
    /// để upload file THẲNG lên Cloudinary, ApiSecret không rời server.
    /// </summary>
    /// <param name="transformation">
    /// Chuỗi transformation Cloudinary tùy chọn (vd "c_fill,w_400,h_400,q_auto,f_auto").
    /// Khi có, phải được client gửi lại NGUYÊN VĂN trong request upload vì nó nằm trong chữ ký.
    /// </param>
    CloudinaryUploadSignature GenerateUploadSignature(string folder, string? transformation = null);
}

public record CloudinaryUploadSignature(
    string CloudName,
    string ApiKey,
    string Signature,
    long Timestamp,
    string Folder,
    string AllowedFormats,
    string? Transformation
);

public class CloudinarySignatureService : ICloudinarySignatureService
{
    // Luôn ký kèm allowed_formats để Cloudinary tự chặn định dạng sai ngay tại server của Cloudinary —
    // đây là cách duy nhất "enforce" định dạng file khi client upload thẳng lên Cloudinary (không qua backend).
    private const string AllowedFormats = "jpg,png,webp";

    private readonly CloudinaryOptions _options;

    public CloudinarySignatureService(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public CloudinaryUploadSignature GenerateUploadSignature(string folder, string? transformation = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Cloudinary yêu cầu: các param gửi kèm (trừ file/cloud_name/resource_type/api_key/signature)
        // sắp xếp alphabet theo tên param -> "key=value&key2=value2" -> nối thẳng ApiSecret -> SHA-1 -> hex.
        var paramsToSign = $"allowed_formats={AllowedFormats}&folder={folder}&timestamp={timestamp}";
        if (!string.IsNullOrWhiteSpace(transformation))
        {
            paramsToSign += $"&transformation={transformation}";
        }
        paramsToSign += _options.ApiSecret;

        var signature = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(paramsToSign))).ToLowerInvariant();

        return new CloudinaryUploadSignature(
            CloudName: _options.CloudName,
            ApiKey: _options.ApiKey,
            Signature: signature,
            Timestamp: timestamp,
            Folder: folder,
            AllowedFormats: AllowedFormats,
            Transformation: transformation
        );
    }
}
