using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pentra.Application.Abstractions;
using Pentra.Web.Models;

namespace Pentra.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly IDashboardService _dashboard;

    public HomeController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var summary = await _dashboard.GetSummaryAsync(ct);
        return View(summary);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
