using System.ComponentModel.DataAnnotations;
using Pentra.Domain.Enums;

namespace Pentra.Web.Models;

/// <summary>Inline form model for adding an authorized scope target.</summary>
public sealed class TargetFormViewModel
{
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Target value is required.")]
    [StringLength(500)]
    [Display(Name = "Target")]
    public string Value { get; set; } = string.Empty;

    [Display(Name = "Kind")]
    public TargetKind Kind { get; set; } = TargetKind.Domain;

    [Display(Name = "Explicitly authorized")]
    public bool IsAuthorized { get; set; }

    [StringLength(2000)]
    public string Notes { get; set; } = string.Empty;
}
