namespace LawyerCaseManager.Models;

/// <summary>
/// Represents a document attached to a case (metadata stored in SQLite; file lives on disk).
/// </summary>
public class DocumentRecord
{
    public int DocID { get; set; }
    public int CaseID { get; set; }
    public string CaseName { get; set; } = string.Empty;
    public string DocName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public string DocType { get; set; } = "Contract";
}
