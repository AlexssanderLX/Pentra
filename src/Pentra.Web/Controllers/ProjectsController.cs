using Microsoft.AspNetCore.Mvc;
using Pentra.Application.Abstractions;
using Pentra.Application.Models;
using Pentra.Web.Models;

namespace Pentra.Web.Controllers;

public sealed class ProjectsController : Controller
{
    private readonly IProjectService _projects;
    private readonly IPhaseService _phases;
    private readonly IToolCatalogService _tools;
    private readonly IChangeHistoryService _history;
    private readonly Pentra.Application.Execution.IExecutionService _executions;
    private readonly Pentra.Application.Execution.IToolAdapterRegistry _adapters;

    public ProjectsController(
        IProjectService projects,
        IPhaseService phases,
        IToolCatalogService tools,
        IChangeHistoryService history,
        Pentra.Application.Execution.IExecutionService executions,
        Pentra.Application.Execution.IToolAdapterRegistry adapters)
    {
        _projects = projects;
        _phases = phases;
        _tools = tools;
        _history = history;
        _executions = executions;
        _adapters = adapters;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _projects.GetListAsync(ct);
        return View(items);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var project = await _projects.GetDetailAsync(id, ct);
        if (project is null)
        {
            return NotFound();
        }

        var phases = await _phases.GetPhasesAsync(ct);
        var catalog = await _tools.GetCatalogAsync(ct);
        var history = await _history.GetForProjectAsync(id, ct);

        var selected = project.ToolSelections
            .Select(s => (s.PhaseId, s.SecurityToolId))
            .ToHashSet();

        var runs = await _executions.GetRunsForProjectAsync(id, ct);

        // Tools that have an execution adapter, plus which ones are active scans.
        var adapterSlugs = _adapters.All.ToDictionary(a => a.Slug, a => a.Definition.IsActiveScan);
        var executableTools = catalog.Where(t => adapterSlugs.ContainsKey(t.Slug)).ToList();
        var activeSlugs = adapterSlugs.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();

        var vm = new ProjectDetailsViewModel
        {
            Project = project,
            Phases = phases,
            Catalog = catalog,
            History = history,
            SelectedToolKeys = selected,
            NewTarget = new TargetFormViewModel { ProjectId = id },
            Runs = runs,
            ExecutableTools = executableTools,
            ActiveToolSlugs = activeSlugs
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create() => View(new ProjectFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(ProjectFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _projects.CreateAsync(new CreateProjectRequest(
            model.Name, model.Client, model.Description, model.Status,
            model.EngagementNotes, model.StartDate, model.EndDate), ct);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["Flash"] = "Project created.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var project = await _projects.GetDetailAsync(id, ct);
        if (project is null)
        {
            return NotFound();
        }

        var model = new ProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Client = project.Client,
            Description = project.Description,
            Status = project.Status,
            EngagementNotes = project.EngagementNotes,
            StartDate = project.StartDate,
            EndDate = project.EndDate
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ProjectFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _projects.UpdateAsync(new UpdateProjectRequest(
            model.Id, model.Name, model.Client, model.Description, model.Status,
            model.EngagementNotes, model.StartDate, model.EndDate), ct);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["Flash"] = "Project updated.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _projects.DeleteAsync(id, ct);
        TempData["Flash"] = result.Succeeded ? "Project deleted." : result.FirstError;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleTool(int id, int phaseId, int toolId, CancellationToken ct)
    {
        var result = await _tools.ToggleSelectionAsync(id, phaseId, toolId, ct);
        if (!result.Succeeded)
        {
            TempData["Flash"] = result.FirstError;
        }

        return RedirectToAction(nameof(Details), "Projects", new { id }, $"phase-{phaseId}");
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
