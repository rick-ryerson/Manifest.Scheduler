namespace Manifest.Scheduler.Domain.GalacticSenate.Entities;

/// <summary>
/// A human participant — student, instructor, or any individual role holder.
/// </summary>
public class Person : Party
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
