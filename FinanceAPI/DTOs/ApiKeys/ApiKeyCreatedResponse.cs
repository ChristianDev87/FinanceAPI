namespace FinanceAPI.DTOs.ApiKeys;

public class ApiKeyCreatedResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty; // plaintext, shown once
    public string CreatedAt { get; set; } = string.Empty;
}
