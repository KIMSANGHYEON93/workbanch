namespace Workbench.Infrastructure.BlobStorage;

public enum FileStorageProvider
{
    /// <summary>Azure Blob Storage. 운영 기본값.</summary>
    AzureBlob = 0,

    /// <summary>로컬 디스크. 개발용 — Azurite 나 실 계정 없이 업로드 경로를 태울 수 있다.</summary>
    Local = 1,
}

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public FileStorageProvider Provider { get; set; } = FileStorageProvider.AzureBlob;

    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "attachments";

    /// <summary>Local 제공자가 파일을 두는 디렉터리.</summary>
    public string LocalRoot { get; set; } = "App_Data/attachments";

    /// <summary>
    /// Development 가 아닌 환경에서 <see cref="FileStorageProvider.Local"/> 을 쓰려면 켜야 한다.
    /// 로컬 디스크는 재배포·스케일아웃에서 첨부가 사라지므로, 실수로 그렇게 되는 것을 막는다.
    /// </summary>
    public bool AllowLocalOutsideDevelopment { get; set; }
}
