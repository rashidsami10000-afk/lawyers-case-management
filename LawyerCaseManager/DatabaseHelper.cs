using System.IO;
using LawyerCaseManager.Models;
using Microsoft.Data.Sqlite;

namespace LawyerCaseManager;

/// <summary>
/// Central SQLite access layer for LawyerCaseManager.
/// Uses parameterized commands only and initializes schema on first use.
/// Database file: lawyers_app.db in the user's local application data folder.
/// </summary>
public static class DatabaseHelper
{
    private static readonly string DbDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LawyerCaseManager");

    private static readonly string DbPath = Path.Combine(DbDirectory, "lawyers_app.db");

    /// <summary>Full path to the SQLite database file (for diagnostics / README).</summary>
    public static string DatabaseFilePath => DbPath;

    private static string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DbPath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    /// <summary>
    /// Ensures the database directory exists, opens the database, and creates all tables if missing.
    /// Call once at application startup.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(DbDirectory);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Clients (
                ClientID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT,
                Address TEXT,
                Email TEXT
            );

            CREATE TABLE IF NOT EXISTS Cases (
                CaseID INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseName TEXT NOT NULL,
                ClientID INTEGER,
                CourtName TEXT,
                CaseNumber TEXT,
                OpeningDate TEXT,
                Status TEXT NOT NULL DEFAULT 'Active',
                Notes TEXT,
                FOREIGN KEY (ClientID) REFERENCES Clients(ClientID) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS Documents (
                DocID INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseID INTEGER NOT NULL,
                DocName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                UploadDate TEXT NOT NULL,
                DocType TEXT NOT NULL,
                FOREIGN KEY (CaseID) REFERENCES Cases(CaseID) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS CaseNotes (
                NoteID INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseID INTEGER NOT NULL,
                NoteText TEXT NOT NULL,
                CreatedDate TEXT NOT NULL,
                Author TEXT,
                FOREIGN KEY (CaseID) REFERENCES Cases(CaseID) ON DELETE CASCADE
            );
            """;
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    #region Clients CRUD

    public static List<ClientRecord> GetAllClients()
    {
        var list = new List<ClientRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ClientID, Name, Phone, Address, Email
            FROM Clients
            ORDER BY Name COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ClientRecord
            {
                ClientID = reader.GetInt32(0),
                Name = reader.GetString(1),
                Phone = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Address = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }
        return list;
    }

    public static int InsertClient(ClientRecord client)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Clients (Name, Phone, Address, Email)
            VALUES ($name, $phone, $address, $email);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", client.Name);
        command.Parameters.AddWithValue("$phone", client.Phone);
        command.Parameters.AddWithValue("$address", client.Address);
        command.Parameters.AddWithValue("$email", client.Email);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static void UpdateClient(ClientRecord client)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Clients
            SET Name = $name, Phone = $phone, Address = $address, Email = $email
            WHERE ClientID = $id;
            """;
        command.Parameters.AddWithValue("$name", client.Name);
        command.Parameters.AddWithValue("$phone", client.Phone);
        command.Parameters.AddWithValue("$address", client.Address);
        command.Parameters.AddWithValue("$email", client.Email);
        command.Parameters.AddWithValue("$id", client.ClientID);
        command.ExecuteNonQuery();
    }

    public static void DeleteClient(int clientId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Clients WHERE ClientID = $id;";
        command.Parameters.AddWithValue("$id", clientId);
        command.ExecuteNonQuery();
    }

    #endregion

    #region Cases CRUD

    public static List<CaseRecord> GetAllCases()
    {
        var list = new List<CaseRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.CaseID, c.CaseName, c.ClientID, IFNULL(cl.Name, ''),
                   c.CourtName, c.CaseNumber, c.OpeningDate, c.Status, c.Notes
            FROM Cases c
            LEFT JOIN Clients cl ON c.ClientID = cl.ClientID
            ORDER BY c.OpeningDate DESC, c.CaseName COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadCaseRow(reader));
        }
        return list;
    }

    public static List<CaseRecord> SearchCases(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return GetAllCases();
        }

        var list = new List<CaseRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var pattern = $"%{keyword.Trim()}%";
        command.CommandText = """
            SELECT c.CaseID, c.CaseName, c.ClientID, IFNULL(cl.Name, ''),
                   c.CourtName, c.CaseNumber, c.OpeningDate, c.Status, c.Notes
            FROM Cases c
            LEFT JOIN Clients cl ON c.ClientID = cl.ClientID
            WHERE c.CaseName LIKE $kw
               OR c.CourtName LIKE $kw
               OR c.CaseNumber LIKE $kw
               OR c.Status LIKE $kw
               OR c.Notes LIKE $kw
               OR IFNULL(cl.Name, '') LIKE $kw
               OR c.OpeningDate LIKE $kw
            ORDER BY c.OpeningDate DESC, c.CaseName COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$kw", pattern);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadCaseRow(reader));
        }
        return list;
    }

    public static List<CaseRecord> GetCasesByDate(DateTime date)
    {
        var list = new List<CaseRecord>();
        var dateKey = date.ToString("yyyy-MM-dd");
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.CaseID, c.CaseName, c.ClientID, IFNULL(cl.Name, ''),
                   c.CourtName, c.CaseNumber, c.OpeningDate, c.Status, c.Notes
            FROM Cases c
            LEFT JOIN Clients cl ON c.ClientID = cl.ClientID
            WHERE c.OpeningDate = $d
            ORDER BY c.CaseName COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$d", dateKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadCaseRow(reader));
        }
        return list;
    }

    public static List<DateTime> GetDatesWithCases()
    {
        var dates = new List<DateTime>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT OpeningDate FROM Cases
            WHERE OpeningDate IS NOT NULL AND OpeningDate <> '';
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var text = reader.GetString(0);
            if (DateTime.TryParse(text, out var dt))
            {
                dates.Add(dt.Date);
            }
        }
        return dates;
    }

    public static int InsertCase(CaseRecord caseRecord)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Cases (CaseName, ClientID, CourtName, CaseNumber, OpeningDate, Status, Notes)
            VALUES ($name, $clientId, $court, $number, $opening, $status, $notes);
            SELECT last_insert_rowid();
            """;
        BindCaseParameters(command, caseRecord);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static void UpdateCase(CaseRecord caseRecord)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Cases
            SET CaseName = $name, ClientID = $clientId, CourtName = $court,
                CaseNumber = $number, OpeningDate = $opening, Status = $status, Notes = $notes
            WHERE CaseID = $id;
            """;
        BindCaseParameters(command, caseRecord);
        command.Parameters.AddWithValue("$id", caseRecord.CaseID);
        command.ExecuteNonQuery();
    }

    public static void DeleteCase(int caseId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Cases WHERE CaseID = $id;";
        command.Parameters.AddWithValue("$id", caseId);
        command.ExecuteNonQuery();
    }

    private static void BindCaseParameters(SqliteCommand command, CaseRecord caseRecord)
    {
        command.Parameters.AddWithValue("$name", caseRecord.CaseName);
        command.Parameters.AddWithValue("$clientId",
            caseRecord.ClientID.HasValue ? caseRecord.ClientID.Value : DBNull.Value);
        command.Parameters.AddWithValue("$court", caseRecord.CourtName);
        command.Parameters.AddWithValue("$number", caseRecord.CaseNumber);
        command.Parameters.AddWithValue("$opening",
            caseRecord.OpeningDate.HasValue
                ? caseRecord.OpeningDate.Value.ToString("yyyy-MM-dd")
                : DBNull.Value);
        command.Parameters.AddWithValue("$status", caseRecord.Status);
        command.Parameters.AddWithValue("$notes", caseRecord.Notes);
    }

    private static CaseRecord ReadCaseRow(SqliteDataReader reader)
    {
        DateTime? opening = null;
        if (!reader.IsDBNull(6))
        {
            var openingText = reader.GetString(6);
            if (DateTime.TryParse(openingText, out var parsed))
            {
                opening = parsed;
            }
        }

        return new CaseRecord
        {
            CaseID = reader.GetInt32(0),
            CaseName = reader.GetString(1),
            ClientID = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            ClientName = reader.GetString(3),
            CourtName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            CaseNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            OpeningDate = opening,
            Status = reader.GetString(7),
            Notes = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
        };
    }

    #endregion

    #region Documents CRUD

    public static List<DocumentRecord> GetAllDocuments()
    {
        var list = new List<DocumentRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.DocID, d.CaseID, IFNULL(c.CaseName, ''), d.DocName, d.FilePath, d.UploadDate, d.DocType
            FROM Documents d
            LEFT JOIN Cases c ON d.CaseID = c.CaseID
            ORDER BY d.UploadDate DESC;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadDocumentRow(reader));
        }
        return list;
    }

    public static List<DocumentRecord> GetDocumentsByCase(int caseId)
    {
        var list = new List<DocumentRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.DocID, d.CaseID, IFNULL(c.CaseName, ''), d.DocName, d.FilePath, d.UploadDate, d.DocType
            FROM Documents d
            LEFT JOIN Cases c ON d.CaseID = c.CaseID
            WHERE d.CaseID = $caseId
            ORDER BY d.UploadDate DESC;
            """;
        command.Parameters.AddWithValue("$caseId", caseId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadDocumentRow(reader));
        }
        return list;
    }

    public static int InsertDocument(DocumentRecord document)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Documents (CaseID, DocName, FilePath, UploadDate, DocType)
            VALUES ($caseId, $name, $path, $upload, $type);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$caseId", document.CaseID);
        command.Parameters.AddWithValue("$name", document.DocName);
        command.Parameters.AddWithValue("$path", document.FilePath);
        command.Parameters.AddWithValue("$upload", document.UploadDate.ToString("o"));
        command.Parameters.AddWithValue("$type", document.DocType);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static void UpdateDocument(DocumentRecord document)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Documents
            SET CaseID = $caseId, DocName = $name, FilePath = $path, UploadDate = $upload, DocType = $type
            WHERE DocID = $id;
            """;
        command.Parameters.AddWithValue("$caseId", document.CaseID);
        command.Parameters.AddWithValue("$name", document.DocName);
        command.Parameters.AddWithValue("$path", document.FilePath);
        command.Parameters.AddWithValue("$upload", document.UploadDate.ToString("o"));
        command.Parameters.AddWithValue("$type", document.DocType);
        command.Parameters.AddWithValue("$id", document.DocID);
        command.ExecuteNonQuery();
    }

    public static void DeleteDocument(int docId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Documents WHERE DocID = $id;";
        command.Parameters.AddWithValue("$id", docId);
        command.ExecuteNonQuery();
    }

    private static DocumentRecord ReadDocumentRow(SqliteDataReader reader)
    {
        DateTime upload = DateTime.Now;
        if (!reader.IsDBNull(5))
        {
            DateTime.TryParse(reader.GetString(5), out upload);
        }

        return new DocumentRecord
        {
            DocID = reader.GetInt32(0),
            CaseID = reader.GetInt32(1),
            CaseName = reader.GetString(2),
            DocName = reader.GetString(3),
            FilePath = reader.GetString(4),
            UploadDate = upload,
            DocType = reader.GetString(6)
        };
    }

    #endregion

    #region CaseNotes CRUD

    public static List<CaseNoteRecord> GetAllCaseNotes()
    {
        var list = new List<CaseNoteRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.NoteID, n.CaseID, IFNULL(c.CaseName, ''), n.NoteText, n.CreatedDate, n.Author
            FROM CaseNotes n
            LEFT JOIN Cases c ON n.CaseID = c.CaseID
            ORDER BY n.CreatedDate DESC;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadNoteRow(reader));
        }
        return list;
    }

    public static List<CaseNoteRecord> GetCaseNotesByCase(int caseId)
    {
        var list = new List<CaseNoteRecord>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.NoteID, n.CaseID, IFNULL(c.CaseName, ''), n.NoteText, n.CreatedDate, n.Author
            FROM CaseNotes n
            LEFT JOIN Cases c ON n.CaseID = c.CaseID
            WHERE n.CaseID = $caseId
            ORDER BY n.CreatedDate DESC;
            """;
        command.Parameters.AddWithValue("$caseId", caseId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadNoteRow(reader));
        }
        return list;
    }

    public static int InsertCaseNote(CaseNoteRecord note)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CaseNotes (CaseID, NoteText, CreatedDate, Author)
            VALUES ($caseId, $text, $created, $author);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$caseId", note.CaseID);
        command.Parameters.AddWithValue("$text", note.NoteText);
        command.Parameters.AddWithValue("$created", note.CreatedDate.ToString("o"));
        command.Parameters.AddWithValue("$author", note.Author);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static void UpdateCaseNote(CaseNoteRecord note)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE CaseNotes
            SET CaseID = $caseId, NoteText = $text, CreatedDate = $created, Author = $author
            WHERE NoteID = $id;
            """;
        command.Parameters.AddWithValue("$caseId", note.CaseID);
        command.Parameters.AddWithValue("$text", note.NoteText);
        command.Parameters.AddWithValue("$created", note.CreatedDate.ToString("o"));
        command.Parameters.AddWithValue("$author", note.Author);
        command.Parameters.AddWithValue("$id", note.NoteID);
        command.ExecuteNonQuery();
    }

    public static void DeleteCaseNote(int noteId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CaseNotes WHERE NoteID = $id;";
        command.Parameters.AddWithValue("$id", noteId);
        command.ExecuteNonQuery();
    }

    private static CaseNoteRecord ReadNoteRow(SqliteDataReader reader)
    {
        DateTime created = DateTime.Now;
        if (!reader.IsDBNull(4))
        {
            DateTime.TryParse(reader.GetString(4), out created);
        }

        return new CaseNoteRecord
        {
            NoteID = reader.GetInt32(0),
            CaseID = reader.GetInt32(1),
            CaseName = reader.GetString(2),
            NoteText = reader.GetString(3),
            CreatedDate = created,
            Author = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
        };
    }

    #endregion
}
