using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

public interface ISearchService
{
    Task<SearchResults> SearchAsync(string? query, CancellationToken cancellationToken = default);
}
