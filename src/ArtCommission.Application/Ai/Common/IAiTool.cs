using System.Text.Json;

namespace ArtCommission.Application.Ai.Common;

/// <summary>
/// Một hàm tra cứu dữ liệu công khai mà AI Assistant được phép gọi khi cần dữ liệu thật.
/// </summary>
public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    object ParametersSchema { get; }
    Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default);
}
