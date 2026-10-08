using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pentra.Application.Execution;
using Pentra.Domain.Abstractions;
using Pentra.Web.Models;

namespace Pentra.Web.Controllers;

public sealed class ExecutionsController : Controller
{
    private readonly IExecutionService _executions;
    private readonly Pentra.Application.Abstractions.IToolCatalogService _catalog;

    public ExecutionsController(IExecutionService executions, Pentra.Application.Abstractions.IToolCatalogService catalog)
    {
        _executions = executions;
        _catalog = catalog;
    }

    [HttpPost]
    public async Task<IActionResult> Start(StartRunForm form, CancellationToken ct)
    {
        var catalog = await _catalog.GetCatalogAsync(ct);
        var slug = catalog.FirstOrDefault(t => t.Id == form.SecurityToolId)?.Slug ?? string.Empty;
        var parameters = BuildParameters(form, slug);

        var result = await _executions.RequestRunAsync(new RequestRunInput(
            form.ProjectId, form.PhaseId, form.SecurityToolId, form.ScopeTargetId, parameters, form.ConfirmedActive), ct);

        if (!result.Succeeded)
        {
            TempData["Flash"] = result.FirstError;
            return RedirectToAction("Details", "Projects", new { id = form.ProjectId }, "executions");
        }

        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var run = await _executions.GetRunAsync(id, ct);
        if (run is null)
        {
            return NotFound();
        }

        var logs = await _executions.GetLogDeltaAsync(id, 0, ct);
        ToolParseResult? parsed = null;
        if (!string.IsNullOrWhiteSpace(run.ResultJson))
        {
            try { parsed = JsonSerializer.Deserialize<ToolParseResult>(run.ResultJson); }
            catch (JsonException) { /* leave null */ }
        }

        return View(new RunDetailsViewModel { Run = run, Logs = logs, Result = parsed });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, int projectId, CancellationToken ct)
    {
        var result = await _executions.RequestCancelAsync(id, ct);
        TempData["Flash"] = result.Succeeded ? "Cancellation requested." : result.FirstError;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Server-Sent Events stream of new log lines and status for a run. One-way,
    /// read-only; terminates when the run reaches a terminal state.
    /// </summary>
    [HttpGet]
    public async Task Stream(int id, int afterSeq, CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var lastSeq = afterSeq;

        while (!ct.IsCancellationRequested)
        {
            var run = await _executions.GetRunAsync(id, ct);
            if (run is null)
            {
                break;
            }

            var logs = await _executions.GetLogDeltaAsync(id, lastSeq, ct);
            foreach (var line in logs)
            {
                lastSeq = line.Seq;
                var payload = JsonSerializer.Serialize(new { seq = line.Seq, stream = line.Stream.ToString(), text = line.Text });
                await WriteEventAsync("log", payload, ct);
            }

            await WriteEventAsync("status", JsonSerializer.Serialize(new { status = run.Status.ToString() }), ct);

            if (!run.IsActive)
            {
                break; // terminal state reached
            }

            try { await Task.Delay(TimeSpan.FromSeconds(1), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task WriteEventAsync(string eventName, string data, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {eventName}\n", ct);
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// Selects only the parameters relevant to the chosen tool, server-side, so
    /// posting the shared form can never inject parameters for another tool.
    /// </summary>
    private static Dictionary<string, string> BuildParameters(StartRunForm form, string slug)
    {
        var parameters = new Dictionary<string, string>();

        switch (slug)
        {
            case "nmap":
                if (form.NmapServiceDetection) parameters["serviceDetection"] = "true";
                if (form.NmapTopPorts is int tp) parameters["topPorts"] = tp.ToString();
                if (!string.IsNullOrWhiteSpace(form.NmapTiming)) parameters["timing"] = form.NmapTiming!;
                break;
            case "httpx":
                parameters["techDetect"] = form.HttpxTechDetect ? "true" : "false";
                if (form.HttpxFollowRedirects) parameters["followRedirects"] = "true";
                break;
            case "subfinder":
                if (form.SubfinderAllSources) parameters["allSources"] = "true";
                break;
        }

        return parameters;
    }
}
