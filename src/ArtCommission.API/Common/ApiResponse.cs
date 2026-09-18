namespace ArtCommission.API.Common;

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public object? Meta { get; set; }
    public object? Error { get; set; }

    public ApiResponse(T? data, object? meta = null, object? error = null)
    {
        Data = data;
        Meta = meta;
        Error = error;
    }
}
