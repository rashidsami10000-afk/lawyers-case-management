namespace LawyerCaseManager.Models;

/// <summary>
/// Represents a client row from the Clients table.
/// </summary>
public class ClientRecord
{
    public int ClientID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
