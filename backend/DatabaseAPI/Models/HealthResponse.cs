namespace DatabaseAPI.Models;

public class HealthResponse
{
    public string Status { get; set; } = "";
    public string Service { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = "";
}
