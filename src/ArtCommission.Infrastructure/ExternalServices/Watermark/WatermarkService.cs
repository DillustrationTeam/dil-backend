using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ArtCommission.Infrastructure.ExternalServices.Watermark;

public class WatermarkService : IWatermarkService
{
    private readonly IArtworkWatermarkService _dwtWatermark;

    public WatermarkService(IArtworkWatermarkService dwtWatermark)
    {
        _dwtWatermark = dwtWatermark;
    }

    public async Task<Stream> ApplyWatermarkAsync(Stream imageStream, string watermarkText, CancellationToken cancellationToken = default)
    {
        imageStream.Position = 0;
        using var resized = new MemoryStream();
        using (var image = await Image.LoadAsync(imageStream, cancellationToken))
        {
            if (image.Width > 1920)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(1920, 0),
                    Mode = ResizeMode.Max
                }));
            }
            await image.SaveAsPngAsync(resized, cancellationToken);
        }
        resized.Position = 0;

        ulong payload;
        do
        {
            payload = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        } while (payload == 0);
        var result = await _dwtWatermark.EmbedInvisibleWatermarkAsync(resized, payload, watermarkText, cancellationToken);
        return result.ProcessedStream;
    }
}
