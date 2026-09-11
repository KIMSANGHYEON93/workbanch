using Workbench.Application;
using Workbench.Infrastructure;
using Workbench.Infrastructure.BlobStorage;
using Workbench.Web.Components;
using Workbench.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddApplication();
builder.Services.Configure<AttachmentOptions>(
    builder.Configuration.GetSection(AttachmentOptions.SectionName));
builder.Services.AddFileStorage(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddWorkbenchAuthentication(builder.Configuration, builder.Environment);

// 파이프라인이 정적이라 상태가 없다 — 요청마다 새로 만들 이유가 없다.
builder.Services.AddSingleton<MarkdownRenderer>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// 브라우저가 Content-Type 을 무시하고 내용을 추측하면, 첨부로 내려보낸 파일이 문서로 실행될 수 있다.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";

    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.UseWorkbenchAuthentication();

app.MapAttachmentEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// SQL Server 구성에서는 즉시 반환한다 — 데모 프로바이더에서만 스키마와 예시 데이터를 세운다.
await app.InitializeDemoDatabaseAsync();

app.Run();
