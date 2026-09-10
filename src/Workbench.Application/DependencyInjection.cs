using Microsoft.Extensions.DependencyInjection;
using Workbench.Application.Interfaces;
using Workbench.Application.Services;

namespace Workbench.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IPageService, PageService>();

        return services;
    }
}
