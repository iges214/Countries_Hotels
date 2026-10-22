using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Common.QuerableXtensions.Paging;

namespace NoName_Hotel.Common.QuerableXtensions.Extensions;

    public static class QueryableExtensions
    {
    public static async Task<PageResult<T>> ToPageResultAsync<T>(
        this IQueryable<T> source, PaginationParameters paginationParameters)
    {
        var totalCount = await source.CountAsync();
        var items = await source
            .Skip((paginationParameters.PageNumber - 1) * paginationParameters.PageSize)
            .Take(paginationParameters.PageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)paginationParameters.PageSize);

        var metaData = new PaginationMetadata
        {
            CurrentPage = paginationParameters.PageNumber,
            PageSize = paginationParameters.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = paginationParameters.PageNumber < totalPages,
            HasPrevious = paginationParameters.PageNumber > 1
        };

        return new PageResult<T>
        {
            Data = items,
            Metadata = metaData
        };
    }
    }

