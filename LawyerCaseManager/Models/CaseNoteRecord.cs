namespace LawyerCaseManager.Models;

/// <summary>
/// Represents a chronological note entry tied to a case.
/// </summary>
public class CaseNoteRecord
{
    public int NoteID { get; set; }
    public int CaseID { get; set; }
    public string CaseName { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string Author { get; set; } = string.Empty;
}
