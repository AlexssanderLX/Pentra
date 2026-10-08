using System.ComponentModel.DataAnnotations;
using Pentra.Domain.Enums;

namespace Pentra.Web.Models;

/// <summary>Create/edit form model for a project, with MVC validation attributes.</summary>
public sealed class ProjectFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Project name is required.")]
    [StringLength(200, ErrorMessage = "Name must be at most 200 characters.")]
    [Display(Name = "Project name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Client is required.")]
    [StringLength(200)]
    public string Client { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    [StringLength(8000)]
    [Display(Name = "Engagement notes / authorization")]
    public string EngagementNotes { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateOnly? EndDate { get; set; }

    public bool IsEdit => Id > 0;
}
