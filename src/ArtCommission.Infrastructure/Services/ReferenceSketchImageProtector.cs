using System.Numerics;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ArtCommission.Infrastructure.Services;

/// <summary>
/// Makes an AI reference sketch unattractive to reuse outside the commission:
/// downscales it, snaps it to 4 flat storyboard tones, and stamps a visible tiled diagonal
/// "AI REFERENCE" watermark across the whole image plus a corner badge.
/// The invisible DWT watermark is applied afterwards by <c>IWatermarkService</c>.
/// </summary>
public class ReferenceSketchImageProtector
{
    /// <summary>Longest side after downscale. 1024x1536 from OpenAI becomes 512x768.</summary>
    public const int MaxLongSide = 768;

    private const string TileText = "AI REFERENCE · dillustration";
    private const string BadgeText = "AI REFERENCE — NOT FINAL ART";
    private const float RotationRadians = -0.52f; // ~ -30 degrees

    private static readonly Color TileColor = Color.FromRgba(36, 31, 26, 52);
    private static readonly Color HatchColor = Color.FromRgba(36, 31, 26, 34);
    private static readonly Color BadgeBackground = Color.FromRgba(36, 31, 26, 190);
    private static readonly Color BadgeForeground = Color.FromRgba(255, 252, 246, 255);

    private readonly ILogger<ReferenceSketchImageProtector> _logger;

    public ReferenceSketchImageProtector(ILogger<ReferenceSketchImageProtector> logger)
    {
        _logger = logger;
    }

    public async Task<MemoryStream> ProtectAsync(byte[] png, CancellationToken cancellationToken = default)
    {
        using var image = Image.Load<Rgba32>(png);

        image.Mutate(ctx => ctx
            .Resize(new ResizeOptions { Size = new Size(MaxLongSide, MaxLongSide), Mode = ResizeMode.Max }));
        SimplifyToStoryboardTones(image);

        var family = TryGetFontFamily();
        if (family is { } f)
        {
            DrawTiledText(image, f);
            DrawBadge(image, f);
        }
        else
        {
            // Container without fonts: still mark the whole image with a diagonal hatch.
            _logger.LogWarning("No system font found; AI reference sketch gets a hatch watermark without text.");
            DrawHatch(image);
        }

        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        return output;
    }

    /// <summary>The only 4 colors a protected sketch may contain (before the watermark is drawn).</summary>
    public static readonly Rgba32 LineTone = new(43, 86, 114);
    public static readonly Rgba32 ShadowTone = new(196, 196, 196);
    public static readonly Rgba32 BackdropTone = new(230, 230, 230);
    public static readonly Rgba32 PaperTone = new(255, 255, 255);

    /// <summary>
    /// Forces the storyboard look whatever the model drew: every pixel is snapped to one of 4 flat
    /// tones (navy line, mid-gray shadow, light-gray backdrop, white) by luminance, so color fills,
    /// gradients and fine texture cannot survive. Dark areas thicker than a stroke (a filled coat,
    /// black hair, a dark pillar) become mid-gray with a navy outline, so only real lines stay navy.
    /// </summary>
    public static void SimplifyToStoryboardTones(Image<Rgba32> image)
    {
        // Median removes speckle from blurry / noisy regions (it would snap into dotted noise),
        // then a light blur merges hatching / texture into flat areas before snapping.
        image.Mutate(ctx => ctx.MedianBlur(3, true).GaussianBlur(0.7f));

        var w = image.Width;
        var h = image.Height;
        var lum = new float[w * h];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                {
                    var p = row[x];
                    lum[y * w + x] = (0.299f * p.R + 0.587f * p.G + 0.114f * p.B) / 255f;
                }
            }
        });

        // Auto-levels: stretch the 1st..99th luminance percentile to 0..1, so a sketch the model
        // drew on a dark tinted background (night scenes) still separates lines from paper.
        var sorted = (float[])lum.Clone();
        Array.Sort(sorted);
        var low = sorted[(int)(sorted.Length * 0.01f)];
        var high = sorted[(int)(sorted.Length * 0.99f)];
        if (high - low > 0.15f)
        {
            for (var i = 0; i < lum.Length; i++) lum[i] = Math.Clamp((lum[i] - low) / (high - low), 0f, 1f);
        }

        var dark = new bool[w * h];
        for (var i = 0; i < dark.Length; i++) dark[i] = lum[i] < 0.42f;

        // Morphological opening with a square window: a dark pixel is part of a "mass" when a
        // fully dark (2r+1)^2 window covers it. Strokes thinner than the window are not masses.
        // Window must be wider than the model's bold outlines (~5-8px at 768) or they get hollowed out.
        var r = Math.Max(3, Math.Max(w, h) / 128);
        var interior = new bool[w * h];
        var darkSums = BoxCounts(dark, w, h, r);
        for (var i = 0; i < interior.Length; i++) interior[i] = darkSums[i].Count == darkSums[i].Area;
        var interiorSums = BoxCounts(interior, w, h, r);
        var mass = new bool[w * h];
        for (var i = 0; i < mass.Length; i++) mass[i] = dark[i] && interiorSums[i].Count > 0;
        var massSums = BoxCounts(mass, w, h, 1);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (mass[i])
                    {
                        // Edge of a dark mass keeps a navy outline, the inside becomes flat shadow.
                        row[x] = massSums[i].Count < massSums[i].Area ? LineTone : ShadowTone;
                        continue;
                    }

                    row[x] = lum[i] switch
                    {
                        < 0.42f => LineTone,
                        < 0.72f => ShadowTone,
                        < 0.92f => BackdropTone,
                        _ => PaperTone,
                    };
                }
            }
        });
    }

    /// <summary>Per pixel: how many mask pixels are in the (2r+1)^2 window, and the window's area clipped to the image.</summary>
    private static (int Count, int Area)[] BoxCounts(bool[] mask, int w, int h, int r)
    {
        var integral = new int[(w + 1) * (h + 1)];
        for (var y = 0; y < h; y++)
        {
            var rowSum = 0;
            for (var x = 0; x < w; x++)
            {
                rowSum += mask[y * w + x] ? 1 : 0;
                integral[(y + 1) * (w + 1) + x + 1] = integral[y * (w + 1) + x + 1] + rowSum;
            }
        }

        var result = new (int, int)[w * h];
        for (var y = 0; y < h; y++)
        {
            var y0 = Math.Max(0, y - r);
            var y1 = Math.Min(h, y + r + 1);
            for (var x = 0; x < w; x++)
            {
                var x0 = Math.Max(0, x - r);
                var x1 = Math.Min(w, x + r + 1);
                var count = integral[y1 * (w + 1) + x1] - integral[y0 * (w + 1) + x1]
                          - integral[y1 * (w + 1) + x0] + integral[y0 * (w + 1) + x0];
                result[y * w + x] = (count, (y1 - y0) * (x1 - x0));
            }
        }

        return result;
    }

    private static FontFamily? TryGetFontFamily()
    {
        foreach (var name in new[] { "DejaVu Sans", "Arial", "Segoe UI", "Liberation Sans" })
        {
            if (SystemFonts.TryGet(name, out var family))
            {
                return family;
            }
        }

        return SystemFonts.Collection.Families.Any() ? SystemFonts.Collection.Families.First() : null;
    }

    private static void DrawTiledText(Image<Rgba32> image, FontFamily family)
    {
        var size = Math.Max(13f, image.Width * 0.034f);
        var font = family.CreateFont(size, FontStyle.Bold);
        var textSize = TextMeasurer.MeasureSize(TileText, new TextOptions(font));
        var stepX = textSize.Width + size * 2.2f;
        var stepY = textSize.Height + size * 3.2f;

        var center = new Vector2(image.Width / 2f, image.Height / 2f);
        var reach = (float)Math.Sqrt(image.Width * image.Width + image.Height * image.Height);

        image.Mutate(ctx =>
        {
            ctx.SetDrawingTransform(Matrix3x2.CreateRotation(RotationRadians, center));
            var row = 0;
            for (var y = center.Y - reach; y < center.Y + reach; y += stepY, row++)
            {
                var offset = row % 2 == 0 ? 0 : stepX / 2;
                for (var x = center.X - reach - offset; x < center.X + reach; x += stepX)
                {
                    ctx.DrawText(TileText, font, TileColor, new PointF(x, y));
                }
            }
            ctx.SetDrawingTransform(Matrix3x2.Identity);
        });
    }

    private static void DrawBadge(Image<Rgba32> image, FontFamily family)
    {
        var size = Math.Max(11f, image.Width * 0.026f);
        var font = family.CreateFont(size, FontStyle.Bold);
        var textSize = TextMeasurer.MeasureSize(BadgeText, new TextOptions(font));
        var pad = size * 0.6f;
        var margin = size * 0.8f;
        var rect = new RectangleF(
            margin,
            image.Height - margin - textSize.Height - pad * 2,
            textSize.Width + pad * 2,
            textSize.Height + pad * 2);

        image.Mutate(ctx =>
        {
            ctx.Fill(BadgeBackground, rect);
            ctx.DrawText(BadgeText, font, BadgeForeground, new PointF(rect.X + pad, rect.Y + pad));
        });
    }

    private static void DrawHatch(Image<Rgba32> image)
    {
        var spacing = Math.Max(24f, image.Width / 12f);
        image.Mutate(ctx =>
        {
            for (var x = -image.Height; x < image.Width; x += (int)spacing)
            {
                ctx.DrawLine(HatchColor, 2f, new PointF(x, image.Height), new PointF(x + image.Height, 0));
            }
        });
    }
}
