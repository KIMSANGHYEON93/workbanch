namespace Workbench.Application;

public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    /// <summary>
    /// 첨부 1건의 최대 크기(바이트). 기본 25MB — 스크린샷·로그를 담되 서버 메모리와 요청 시간을 지킨다.
    /// 화면의 업로드 상한과 서버 검증이 이 값 하나를 함께 본다.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
}
