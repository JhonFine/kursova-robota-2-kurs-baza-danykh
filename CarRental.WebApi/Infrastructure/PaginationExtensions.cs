using Microsoft.AspNetCore.Http;

namespace CarRental.WebApi.Infrastructure;

internal readonly record struct PaginationOptions(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}

internal static class PaginationExtensions
{
    private const int DefaultPage = 1;
    private const int MaxPageSize = 200;

    public static PaginationOptions? Normalize(int? page, int? pageSize)
    {
        if (!page.HasValue && !pageSize.HasValue)
        {
            return null;
        }

        var normalizedPage = Math.Max(page ?? DefaultPage, DefaultPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, MaxPageSize);
        return new PaginationOptions(normalizedPage, normalizedPageSize);
    }

    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, PaginationOptions? options)
    {
        if (!options.HasValue)
        {
            return query;
        }

        return query
            .Skip(options.Value.Skip)
            .Take(options.Value.PageSize);
    }

    public static void ApplyPaginationHeaders(this HttpResponse response, PaginationOptions options, int totalCount)
    {
        response.Headers["X-Page"] = options.Page.ToString();
        response.Headers["X-Page-Size"] = options.PageSize.ToString();
        response.Headers["X-Total-Count"] = totalCount.ToString();
    }
}
