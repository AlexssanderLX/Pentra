using Microsoft.Extensions.DependencyInjection;
using Pentra.Application.Abstractions;
using Pentra.Application.Security;
using Pentra.Application.Services;
using Pentra.Domain.Abstractions;

namespace Pentra.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IChangeHistoryService, ChangeHistoryService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITargetService, TargetService>();
        services.AddScoped<IToolCatalogService, ToolCatalogService>();
        services.AddScoped<IPhaseService, PhaseService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Security: execution is denied by default in this checkpoint.
        services.AddSingleton<IToolExecutionPolicy, DenyAllToolExecutionPolicy>();

        return services;
    }
}
