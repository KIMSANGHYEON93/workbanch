using Microsoft.Extensions.Options;
using Workbench.Application.Interfaces;

namespace Workbench.Infrastructure.BlobStorage;

/// <summary>
/// 로컬 디스크 구현. 개발에서 업로드·다운로드 경로를 실제로 태우기 위한 것이다.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.LocalRoot);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var blobPath = Domain.BlobPath.Create(Guid.NewGuid(), fileName);
        var fullPath = Resolve(blobPath)
            ?? throw new InvalidOperationException("생성된 저장 경로가 저장소 밖을 가리킵니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);

        return blobPath;
    }

    public Task<Stream?> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(blobPath);

        return Task.FromResult<Stream?>(
            fullPath is null || !File.Exists(fullPath) ? null : File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(blobPath);

        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 경로가 저장소 루트 밖을 가리키면 <c>null</c>. 경로는 우리가 만들지만, DB 의 값이 어떤
    /// 경위로든 바뀌었을 때 그 값으로 임의 파일을 읽거나 지우는 일이 없어야 한다.
    /// </summary>
    private string? Resolve(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath) || Path.IsPathRooted(blobPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, blobPath));

        return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? fullPath
            : null;
    }
}
