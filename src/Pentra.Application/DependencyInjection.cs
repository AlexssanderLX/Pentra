using Microsoft.Extensions.DependencyInjection;
using Pentra.Application.Abstractions;
using Pentra.Application.Execution;
using Pentra.Application.Execution.Adapters;
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

        // Tool adapters (pure: build argv + parse output) and their registry.
        services.AddSingleton<IToolAdapter, NmapAdapter>();
        services.AddSingleton<IToolAdapter, SubfinderAdapter>();
        services.AddSingleton<IToolAdapter, HttpxAdapter>();
        services.AddSingleton<IToolAdapterRegistry, ToolAdapterRegistry>();

        // Execution authorization: deny-by-default, scope-aware policy.
        services.AddSingleton<IToolExecutionPolicy, ScopedToolExecutionPolicy>();

        return services;
    }
}
