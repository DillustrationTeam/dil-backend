using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArtCommission.Application.Commission.Interfaces;

public interface IStorageService
{
    Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default);
    Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default);
    string GeneratePresignedDownloadUrl(string privateKey, TimeSpan expiry);
}
