using System.Text;
using Microsoft.Extensions.Options;
using Workbench.Infrastructure.BlobStorage;

namespace Workbench.Tests.Infrastructure;

/// <summary>
/// 로컬 저장소는 개발용 구현이지만, 업로드·다운로드 경로를 <b>실제로</b> 태워 볼 수 있는 유일한
/// 구현이다(이 환경에는 Azure 접속 정보도 Azurite 도 없다).
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"workbench-tests-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { LocalRoot = _root }));
    }

    [Fact]
    public async Task SavedBytes_ComeBackUnchanged()
    {
        var original = Encoding.UTF8.GetBytes("배포 로그\n2026-09-10 OK");

        var blobPath = await _storage.SaveAsync(
            new MemoryStream(original), "배포.log", "text/plain");

        await using var read = await _storage.OpenReadAsync(blobPath);
        Assert.NotNull(read);

        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);

        Assert.Equal(original, buffer.ToArray());
    }

    [Fact]
    public async Task SavedFile_LandsInsideTheRootAndNotUnderTheUsersName()
    {
        var blobPath = await _storage.SaveAsync(
            new MemoryStream([1, 2, 3]),
            "../../escape.txt",
            "text/plain");

        Assert.DoesNotContain("..", blobPath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_root, blobPath)));
    }

    [Fact]
    public async Task Delete_RemovesTheFile()
    {
        var blobPath = await _storage.SaveAsync(
            new MemoryStream([1]), "a.bin", "application/octet-stream");

        await _storage.DeleteAsync(blobPath);

        Assert.Null(await _storage.OpenReadAsync(blobPath));
        Assert.False(File.Exists(Path.Combine(_root, blobPath)));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("../../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public async Task PathsOutsideTheRoot_AreNotReadable(string blobPath)
    {
        // 경로는 우리가 만들지만, DB 의 값이 어떤 경위로든 바뀌었을 때 그 값으로 임의 파일을
        // 읽거나 지우는 일이 없어야 한다.
        Assert.Null(await _storage.OpenReadAsync(blobPath));
    }

    [Fact]
    public async Task DeleteOutsideTheRoot_DoesNothing()
    {
        var outsider = Path.Combine(Path.GetTempPath(), $"workbench-outsider-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outsider, "건드리면 안 되는 파일");

        try
        {
            await _storage.DeleteAsync(
                Path.Combine("..", Path.GetFileName(outsider)));

            Assert.True(File.Exists(outsider));
        }
        finally
        {
            File.Delete(outsider);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
