using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Common;

public static class ApiErrors
{
    public static ApiResponse<object> Create(int status, string title, string detail, string traceId,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["traceId"] = traceId;
        if (validationErrors is not null)
            problem.Extensions["errors"] = validationErrors;

        return new ApiResponse<object>(null, null, problem);
    }
}
