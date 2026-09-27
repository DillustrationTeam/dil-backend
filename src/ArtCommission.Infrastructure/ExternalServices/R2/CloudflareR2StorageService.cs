using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using ArtCommission.Application.Commission.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.Infrastructure.ExternalServices.R2;

public class CloudflareR2StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _publicBucket;
    private readonly string _privateBucket;
    private readonly string _publicDomain;

    public CloudflareR2StorageService(IConfiguration configuration)
    {
        var accountId = configuration["CloudflareR2:AccountId"];
        var accessKey = configuration["CloudflareR2:AccessKeyId"];
        var secretKey = configuration["CloudflareR2:SecretAccessKey"];
        _publicBucket = configuration["CloudflareR2:PublicBucket"] ?? "dil-art-public";
        _privateBucket = configuration["CloudflareR2:PrivateBucket"] ?? "dil-art-private-vault";
        _publicDomain = configuration["CloudflareR2:PublicDomain"] ?? "https://pub-xxxx.r2.dev";

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto"
        };
        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _publicBucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _s3Client.PutObjectAsync(request, ct);
        return $"{_publicDomain}/{key}";
    }

    public async Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _privateBucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _s3Client.PutObjectAsync(request, ct);
        return key;
    }

    public string GeneratePresignedDownloadUrl(string privateKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _privateBucket,
            Key = privateKey,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        };
        return _s3Client.GetPreSignedURL(request);
    }
}
