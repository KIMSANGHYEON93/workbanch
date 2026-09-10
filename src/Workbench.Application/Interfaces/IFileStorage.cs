namespace Workbench.Application.Interfaces;

/// <summary>
/// 첨부 파일의 바이트를 보관하는 곳. 경로 생성은 <b>구현이 아니라 호출자</b>가 하지 않는다 —
/// <see cref="SaveAsync"/> 가 안전한 경로를 만들어 돌려준다(사용자 파일명을 경로에 쓰지 않기 위함).
/// </summary>
public interface IFileStorage
{
    /// <returns>이 저장소 안에서 파일을 다시 찾을 수 있는 상대 경로.</returns>
    Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string blobPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
