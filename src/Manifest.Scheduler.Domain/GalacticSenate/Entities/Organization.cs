namespace Manifest.Scheduler.Domain.GalacticSenate.Entities;

/// <summary>
/// An institutional participant — a school, department, or any organizational body.
/// </summary>
public class Organization : Party
{
    public string Name { get; set; } = string.Empty;
}
