using System.Text.Json;
using System.Text.RegularExpressions;
using ArtCommission.Application.ArtistStudio.DTOs;

namespace ArtCommission.Application.ArtistStudio.Workstation;

public static class CreatorAssetValidation
{
    public static string? Error(CreateCreatorAssetRequest asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Name) || asset.Name.Trim().Length > 120 ||
            !new[] { "Pose", "Palette", "Preset" }.Contains(asset.AssetType))
            return "Tên tài nguyên phải từ 1 đến 120 ký tự và loại tài nguyên phải hợp lệ.";
        if (!string.IsNullOrWhiteSpace(asset.AssetUrl) &&
            (asset.AssetUrl.Length > 2048 || !Uri.TryCreate(asset.AssetUrl, UriKind.Absolute, out var uri) ||
             uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)))
            return "Đường dẫn tài nguyên phải là URL HTTP/HTTPS hợp lệ.";
        if (asset.MetadataJson is null || asset.MetadataJson.Length > 16384)
            return "Nội dung tài nguyên không hợp lệ.";
        try
        {
            using var document = JsonDocument.Parse(asset.MetadataJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "Nội dung tài nguyên phải là một đối tượng JSON.";
            var notes = "";
            if (root.TryGetProperty("notes", out var note))
            {
                if (note.ValueKind != JsonValueKind.String) return "Ghi chú không hợp lệ.";
                notes = note.GetString() ?? "";
                if (notes.Length > 3000) return "Ghi chú tối đa 3000 ký tự.";
            }
            if (asset.AssetType == "Palette")
            {
                if (!root.TryGetProperty("colors", out var colors) || colors.ValueKind != JsonValueKind.Array ||
                    colors.GetArrayLength() is < 2 or > 16 || colors.EnumerateArray().Any(x =>
                        x.ValueKind != JsonValueKind.String || !Regex.IsMatch(x.GetString()!, "^#[0-9a-fA-F]{6}$")))
                    return "Bảng màu cần từ 2 đến 16 mã màu HEX dạng #RRGGBB.";
            }
            else if (asset.AssetType == "Pose" && string.IsNullOrWhiteSpace(asset.AssetUrl))
                return "Pose mẫu cần có ảnh tham chiếu.";
            else if (asset.AssetType == "Preset" && string.IsNullOrWhiteSpace(asset.AssetUrl) && string.IsNullOrWhiteSpace(notes))
                return "Preset cần có nội dung hướng dẫn hoặc ảnh tham chiếu.";
        }
        catch (JsonException) { return "Nội dung tài nguyên không phải JSON hợp lệ."; }
        return null;
    }
}
