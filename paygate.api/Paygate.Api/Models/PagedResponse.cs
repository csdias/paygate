namespace Paygate.Api.Models;

public record PagedResponse<T>(IReadOnlyList<T> Items, Guid? NextCursor);
