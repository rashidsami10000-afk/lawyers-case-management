namespace LawyerCaseManager.Models;

/// <summary>
/// Represents a legal case row from the Cases table.
/// </summary>
public class CaseRecord
{
    public int CaseID { get; set; }
    public string CaseName { get; set; } = string.Empty;
    public int? ClientID { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string CourtName { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public DateTime? OpeningDate { get; set; }
    public string Status { get; set; } = "Active";
    public string Notes { get; set; } = string.Empty;
}
