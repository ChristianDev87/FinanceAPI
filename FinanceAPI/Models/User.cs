namespace FinanceAPI.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string RoleName { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public int PasswordVersion { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
