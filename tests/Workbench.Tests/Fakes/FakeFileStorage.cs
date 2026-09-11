using Workbench.Application.Interfaces;
using Workbench.Domain;

namespace Workbench.Tests.Fakes;

public sealed class FakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public List<string> Deleted { get; } = [];

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // 실제 구현과 같은 규칙으로 경로를 만든다 — 서비스가 경로를 짓지 않는다는 계약을 지키기 위함.
        var blobPath = BlobPath.Create(Guid.NewGuid(), fileName);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _files[blobPath] = buffer.ToArray();

        return blobPath;
    }

    public Task<Stream?> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(_files.TryGetValue(blobPath, out var bytes)
            ? new MemoryStream(bytes)
            : null);

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        Deleted.Add(blobPath);
        _files.Remove(blobPath);

        return Task.CompletedTask;
    }
}
