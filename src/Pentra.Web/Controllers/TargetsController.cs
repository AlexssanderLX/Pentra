using Microsoft.AspNetCore.Mvc;
using Pentra.Application.Abstractions;
using Pentra.Application.Models;
using Pentra.Web.Models;

namespace Pentra.Web.Controllers;

public sealed class TargetsController : Controller
{
    private readonly ITargetService _targets;

    public TargetsController(ITargetService targets) => _targets = targets;

    [HttpPost]
    public async Task<IActionResult> Create(TargetFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Flash"] = "Target value is required.";
            return RedirectToAction("Details", "Projects", new { id = model.ProjectId });
        }

        var result = await _targets.AddAsync(new CreateTargetRequest(
            model.ProjectId, model.Value, model.Kind, model.IsAuthorized, model.Notes), ct);

        TempData["Flash"] = result.Succeeded ? "Target added to scope." : result.FirstError;
        return RedirectToAction("Details", "Projects", new { id = model.ProjectId }, fragment: "scope");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, int projectId, CancellationToken ct)
    {
        var result = await _targets.RemoveAsync(id, ct);
        TempData["Flash"] = result.Succeeded ? "Target removed." : result.FirstError;
        return RedirectToAction("Details", "Projects", new { id = projectId }, fragment: "scope");
    }
}
