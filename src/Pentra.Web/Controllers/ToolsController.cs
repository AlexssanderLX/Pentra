using Microsoft.AspNetCore.Mvc;
using Pentra.Application.Abstractions;

namespace Pentra.Web.Controllers;

public sealed class ToolsController : Controller
{
    private readonly IToolCatalogService _tools;
    private readonly IPhaseService _phases;

    public ToolsController(IToolCatalogService tools, IPhaseService phases)
    {
        _tools = tools;
        _phases = phases;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var catalog = await _tools.GetCatalogAsync(ct);
        return View(catalog);
    }

    public async Task<IActionResult> Phases(CancellationToken ct)
    {
        var phases = await _phases.GetPhasesAsync(ct);
        return View(phases);
    }
}
