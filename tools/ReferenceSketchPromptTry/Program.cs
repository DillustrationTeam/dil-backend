// Dev tool: chạy thử ReferenceSketchPromptService (T03) với Gemini thật,
// tùy chọn sinh ảnh bằng OpenAI (--images) để xem prompt cho ra ảnh thế nào.
//
//   dotnet run --project tools/ReferenceSketchPromptTry                 # chỉ prompt (Gemini, gần như miễn phí)
//   dotnet run --project tools/ReferenceSketchPromptTry -- --images     # + sinh ảnh cho các case hợp lệ (tốn vài cent)
//   dotnet run --project tools/ReferenceSketchPromptTry -- --case 1     # chỉ chạy case có tên bắt đầu bằng "1"
//   dotnet run --project tools/ReferenceSketchPromptTry -- --reprotect  # chỉ chạy lại hậu xử lý trên ảnh gốc có sẵn trong out/ (miễn phí)
//   dotnet run --project tools/ReferenceSketchPromptTry -- --hints      # đọc thử nút "🎲 Gợi ý bố cục độc đáo" (T11)
//   dotnet run --project tools/ReferenceSketchPromptTry -- --hints --case 1   # chỉ case bắt đầu bằng "1"
//
// Key đọc từ User Secrets của ArtCommission.API (Gemini:ApiKey, OpenAi:ApiKey) hoặc biến môi trường
// Gemini__ApiKey / OpenAi__ApiKey. Không in key ra màn hình.

using System.Text;
using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Infrastructure.ExternalServices.Gemini;
using ArtCommission.Infrastructure.ExternalServices.OpenAi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;

var withImages = args.Contains("--images");
var hintsMode = args.Contains("--hints");
var caseFilter = args.SkipWhile(a => a != "--case").Skip(1).FirstOrDefault();

if (args.Contains("--reprotect"))
{
    // Re-run only the server post-processing on raw images already in out/ — no API calls, free.
    var dir = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ReferenceSketchPromptTry", "out");
    var protector = new ArtCommission.Infrastructure.Services.ReferenceSketchImageProtector(
        Microsoft.Extensions.Logging.Abstractions.NullLogger<ArtCommission.Infrastructure.Services.ReferenceSketchImageProtector>.Instance);
    foreach (var raw in Directory.GetFiles(dir, "*.png").Where(f => !f.EndsWith("-protected.png")))
    {
        if (caseFilter is not null && !Path.GetFileName(raw).StartsWith(caseFilter)) continue;
        await using var stream = await protector.ProtectAsync(await File.ReadAllBytesAsync(raw));
        var target = raw[..^4] + "-protected.png";
        await using (var fs = File.Create(target)) await stream.CopyToAsync(fs);
        Console.WriteLine($"  {target}");
    }
    return 0;
}

var config = new ConfigurationBuilder()
    .AddUserSecrets<CaseInput>()
    .AddEnvironmentVariables()
    .Build();

if (string.IsNullOrWhiteSpace(config["Gemini:ApiKey"]))
{
    Console.WriteLine("THIEU Gemini:ApiKey. Chay trong dil-backend/src/ArtCommission.API:");
    Console.WriteLine("  dotnet user-secrets set \"Gemini:ApiKey\" \"<key tu aistudio.google.com>\"");
    return 1;
}

if (withImages && string.IsNullOrWhiteSpace(config["OpenAi:ApiKey"]))
{
    Console.WriteLine("THIEU OpenAi:ApiKey (can cho --images).");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
services.Configure<GeminiOptions>(config.GetSection(GeminiOptions.SectionName));
services.Configure<OpenAiOptions>(config.GetSection(OpenAiOptions.SectionName));
services.AddHttpClient<GeminiAiClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
services.AddHttpClient<OpenAiImageClient>(c => c.Timeout = TimeSpan.FromSeconds(120));
services.AddScoped<IAiChatClient>(sp => sp.GetRequiredService<GeminiAiClient>());
services.AddScoped<IAiImageClient>(sp => sp.GetRequiredService<OpenAiImageClient>());
services.AddScoped<ReferenceSketchPromptService>();
services.AddScoped<ArtCommission.Infrastructure.Services.ReferenceSketchImageProtector>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var promptService = scope.ServiceProvider.GetRequiredService<ReferenceSketchPromptService>();
var imageClient = scope.ServiceProvider.GetRequiredService<IAiImageClient>();

var casesPath = Path.Combine(AppContext.BaseDirectory, "cases.json");
var cases = JsonSerializer.Deserialize<CaseInput[]>(
    File.ReadAllText(casesPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

var outDir = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ReferenceSketchPromptTry", "out");
var report = new StringBuilder();

void Log(string line = "")
{
    Console.WriteLine(line);
    report.AppendLine(line);
}

foreach (var c in cases.Where(c => caseFilter is null || c.Name.StartsWith(caseFilter)))
{
    Log($"==================== CASE {c.Name} ====================");
    Log($"Brief      : {c.Brief}");
    Log($"Goi y      : {(string.IsNullOrWhiteSpace(c.LayoutHint) ? "(khong co)" : c.LayoutHint)}");
    Log($"Khong ve   : {c.WontDraw}");
    Log();

    if (hintsMode)
    {
        var techniques = LayoutHintTechniques.PickRandom(3);
        Log($"Ky thuat   : {string.Join(", ", techniques)}");
        var hints = await promptService.SuggestLayoutHintsAsync(c.Brief, c.LayoutHint, c.WontDraw, techniques);
        if (!hints.Success)
        {
            Log($"[HINTS] LOI -> {hints.Error}");
            Log();
            continue;
        }

        for (var i = 0; i < hints.Hints!.Length; i++)
        {
            var h = hints.Hints[i];
            Log($"[HINTS] GOI Y {i + 1} ({h.Technique}): {h.TextVi}");
        }
        Log();
        continue;
    }

    var policy = await promptService.CheckPolicyAsync(c.Brief, c.LayoutHint, c.WillDraw, c.WontDraw);
    if (!policy.Success)
    {
        Log($"[1] KIEM TRA QUY DINH: LOI -> {policy.Error}");
        Log();
        continue;
    }

    Log($"[1] KIEM TRA QUY DINH: {(policy.Allowed ? "CHO PHEP" : "CHAN")}");
    if (!policy.Allowed)
    {
        Log($"    Ly do (VI): {policy.ReasonVi}");
        Log($"    Ly do (EN): {policy.ReasonEn}");
        Log();
        continue;
    }

    var variants = await promptService.BuildVariantsAsync(c.Brief, c.LayoutHint, c.WontDraw);
    if (!variants.Success)
    {
        Log($"[2] PHUONG AN: LOI -> {variants.Error}");
        Log();
        continue;
    }

    for (var i = 0; i < variants.Variants!.Length; i++)
    {
        var v = variants.Variants[i];
        Log($"[2] PHUONG AN {i + 1}: {v.LabelVi}  |  {v.LabelEn}");
        Log($"    pose       : {v.Pose}");
        Log($"    camera     : {v.Camera}");
        Log($"    composition: {v.Composition}");
        Log($"    promptEn   : {v.PromptEn}");

        if (withImages)
        {
            var img = await imageClient.GenerateAsync(v.PromptEn, "1024x1536");
            if (img.Success && img.Png is not null)
            {
                Directory.CreateDirectory(outDir);
                var file = Path.Combine(outDir, $"{c.Name}-phuong-an-{i + 1}.png");
                await File.WriteAllBytesAsync(file, img.Png);
                Log($"    anh goc    : {file}");

                // Same visible protection the real job applies (downscale + soften + tiled watermark).
                var protector = scope.ServiceProvider.GetRequiredService<ArtCommission.Infrastructure.Services.ReferenceSketchImageProtector>();
                await using var protectedStream = await protector.ProtectAsync(img.Png);
                var protectedFile = Path.Combine(outDir, $"{c.Name}-phuong-an-{i + 1}-protected.png");
                await File.WriteAllBytesAsync(protectedFile, protectedStream.ToArray());
                Log($"    anh da bao ve: {protectedFile}");
            }
            else
            {
                Log($"    anh        : LOI -> {(img.ContentBlocked ? "OpenAI chan noi dung" : img.Error)}");
            }
        }
        Log();
    }
}

Directory.CreateDirectory(outDir);
var reportPath = Path.Combine(outDir, "report.txt");
await File.WriteAllTextAsync(reportPath, report.ToString(), Encoding.UTF8);
Console.WriteLine($"Da luu bao cao: {reportPath}");
return 0;

internal sealed record CaseInput(string Name, string Brief, string? LayoutHint, string? WillDraw, string? WontDraw);
