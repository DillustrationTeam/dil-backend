using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArtCommission.Application.Commission.Interfaces;

public interface IWatermarkService
{
    Task<Stream> ApplyWatermarkAsync(Stream imageStream, string watermarkText, CancellationToken cancellationToken = default);
}
