using Workbench.Application.DTOs;

namespace Workbench.Application.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<ProjectDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(
        ProjectEditModel model,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        Guid id,
        ProjectEditModel model,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
