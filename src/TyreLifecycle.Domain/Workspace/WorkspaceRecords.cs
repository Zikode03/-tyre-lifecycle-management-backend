namespace TyreLifecycle.Workspace;

public class WorkspaceDocument
{
    public string Key { get; set; } = "";
    public string Json { get; set; } = "null";
    public long Version { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public class StaffAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string RoleLabel { get; set; } = "";
    public string Branch { get; set; } = "";
    public string? BranchId { get; set; }
    public string? FleetId { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
}
public class PortalChallenge
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool Used { get; set; }
}
