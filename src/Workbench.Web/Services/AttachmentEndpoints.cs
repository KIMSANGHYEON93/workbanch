using Workbench.Application.Interfaces;

namespace Workbench.Web.Services;

public static class AttachmentEndpoints
{
    /// <summary>
    /// 첨부 파일 내려받기. Blazor 컴포넌트가 아니라 엔드포인트인 이유는, 바이트를 회로(circuit)로
    /// 흘려보내지 않고 평범한 HTTP 응답으로 내보내기 위해서다.
    /// </summary>
    public static WebApplication MapAttachmentEndpoints(this WebApplication app)
    {
        app.MapGet("/attachments/{id:guid}", async (
            Guid id,
            IAttachmentService attachments,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var file = await attachments.OpenAsync(id, cancellationToken);

                // ★ fileDownloadName 을 주면 Content-Disposition 이 attachment 가 된다.
                // 이것이 없으면 업로드된 .html·.svg 가 이 앱의 출처(origin)에서 그대로 렌더돼
                // 저장형 XSS 가 된다 — 세션 쿠키가 그대로 딸려 있는 출처다.
                return file is null
                    ? Results.NotFound()
                    : Results.File(file.Content, file.ContentType, file.FileName);
            }
            catch (Exception ex)
            {
                // 저장소나 DB 가 잠깐 죽었을 때 처리되지 않은 예외로 끝내지 않는다 —
                // 개발 환경에서는 스택 트레이스가 그대로 응답에 실린다.
                loggerFactory
                    .CreateLogger(typeof(AttachmentEndpoints))
                    .LogError(ex, "첨부 다운로드 실패 (AttachmentId={AttachmentId})", id);

                return Results.Problem(
                    detail: "첨부 파일을 내려받지 못했습니다. 잠시 후 다시 시도하세요.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .RequireAuthorization();

        return app;
    }
}
