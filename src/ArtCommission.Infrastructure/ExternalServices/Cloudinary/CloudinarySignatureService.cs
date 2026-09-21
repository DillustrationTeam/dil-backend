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
    CloudinaryUploadSignature GenerateUploadSignature(string folder);
}

public record CloudinaryUploadSignature(
    string CloudName,
    string ApiKey,
    string Signature,
    long Timestamp,
    string Folder
);

public class CloudinarySignatureService : ICloudinarySignatureService
{
    private readonly CloudinaryOptions _options;

    public CloudinarySignatureService(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public CloudinaryUploadSignature GenerateUploadSignature(string folder)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Cloudinary yêu cầu: các param gửi kèm (trừ file/cloud_name/resource_type/api_key/signature)
        // sắp xếp alphabet theo tên param -> "key=value&key2=value2" -> nối thẳng ApiSecret -> SHA-1 -> hex.
        var paramsToSign = $"folder={folder}&timestamp={timestamp}{_options.ApiSecret}";
        var signature = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(paramsToSign))).ToLowerInvariant();

        return new CloudinaryUploadSignature(
            CloudName: _options.CloudName,
            ApiKey: _options.ApiKey,
            Signature: signature,
            Timestamp: timestamp,
            Folder: folder
        );
    }
}
