using Workbench.Application;
using Workbench.Infrastructure;
using Workbench.Web.Components;
using Workbench.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
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

app.UseStaticFiles();
app.UseAntiforgery();

app.UseWorkbenchAuthentication();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
