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
        _publicDomain = (configuration["CloudflareR2:PublicDomain"] ?? "https://pub-xxxx.r2.dev").TrimEnd('/');

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
            AutoCloseStream = false,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _s3Client.PutObjectAsync(request, ct);
        return $"{_publicDomain}/{key.TrimStart('/')}";
    }

    public async Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _privateBucket,
            Key = key,
            InputStream = stream,
            AutoCloseStream = false,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _s3Client.PutObjectAsync(request, ct);
        return key;
    }

    public async Task<Stream?> OpenPrivateReadAsync(string privateKey, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_privateBucket, privateKey, ct);
            return new S3OwnedStream(response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public string GeneratePresignedDownloadUrl(string privateKey, TimeSpan expiry)
    {
        var fileName = Path.GetFileName(privateKey);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _privateBucket,
            Key = privateKey,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{Uri.EscapeDataString(fileName)}\""
            }
        };
        return _s3Client.GetPreSignedURL(request);
    }

    private sealed class S3OwnedStream(GetObjectResponse owner) : Stream
    {
        private readonly Stream _inner = owner.ResponseStream;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing)
        {
            if (disposing) owner.Dispose();
            base.Dispose(disposing);
        }
    }
}
