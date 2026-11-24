using Vinex.HR.Domain.Common;

namespace Vinex.HR.Domain.Entities;

public class Position : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional: a position may belong to one department or be general (null).</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;
}
