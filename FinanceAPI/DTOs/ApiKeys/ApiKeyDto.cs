namespace FinanceAPI.DTOs.ApiKeys;

public class ApiKeyDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public int? CreatedByAdminId { get; set; }
}
