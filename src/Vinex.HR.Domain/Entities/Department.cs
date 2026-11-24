using Vinex.HR.Domain.Common;

namespace Vinex.HR.Domain.Entities;

public class Department : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public List<Position> Positions { get; set; } = [];
}
