using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LawyerCaseManager.Models;
using Microsoft.Win32;

namespace LawyerCaseManager;

/// <summary>
/// Main shell for LawyerCaseManager. All view logic lives in code-behind per project requirements.
/// Navigation switches visibility of panel grids; data is loaded from <see cref="DatabaseHelper"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<CaseRecord> _cases = new();
    private readonly ObservableCollection<ClientRecord> _clients = new();
    private readonly ObservableCollection<DocumentRecord> _documents = new();
    private readonly ObservableCollection<CaseNoteRecord> _notes = new();
    private readonly ObservableCollection<CaseRecord> _calendarCases = new();

    private CaseRecord? _editingCase;
    private ClientRecord? _editingClient;
    private bool _suppressCaseSearch;

    public MainWindow()
    {
        InitializeComponent();
        this.EnableWorkAreaAwareMaximize();
        CasesGrid.ItemsSource = _cases;
        ClientsGrid.ItemsSource = _clients;
        DocumentsGrid.ItemsSource = _documents;
        NotesList.ItemsSource = _notes;
        CalendarCasesGrid.ItemsSource = _calendarCases;

        CaseStatusCombo.SelectedIndex = 0;
        DocTypeCombo.SelectedIndex = 0;

        LoadAllData();
        ShowPanel(CasesPanel, NavCases);
    }

    #region Window layout (work area / taskbar)

    /// <summary>Centers the initial window in the work area without capping maximize (handled by Win32 hook).</summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        Width = Math.Min(960, work.Width);
        Height = Math.Min(850, work.Height);
        Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
        Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
    }

    #endregion

    #region Navigation

    private void NavCases_Click(object sender, RoutedEventArgs e) => ShowPanel(CasesPanel, NavCases);
    private void NavClients_Click(object sender, RoutedEventArgs e) => ShowPanel(ClientsPanel, NavClients);
    private void NavDocuments_Click(object sender, RoutedEventArgs e) => ShowPanel(DocumentsPanel, NavDocuments);
    private void NavNotes_Click(object sender, RoutedEventArgs e) => ShowPanel(NotesPanel, NavNotes);
    private void NavCalendar_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(CalendarPanel, NavCalendar);
        RefreshCalendarDay();
    }

    /// <summary>Activates one content panel and highlights the matching sidebar button.</summary>
    private void ShowPanel(UIElement activePanel, Button activeNavButton)
    {
        CasesPanel.Visibility = Visibility.Collapsed;
        ClientsPanel.Visibility = Visibility.Collapsed;
        DocumentsPanel.Visibility = Visibility.Collapsed;
        NotesPanel.Visibility = Visibility.Collapsed;
        CalendarPanel.Visibility = Visibility.Collapsed;

        activePanel.Visibility = Visibility.Visible;

        foreach (var child in NavStack.Children)
        {
            if (child is Button btn)
            {
                btn.Style = (Style)FindResource(
                    ReferenceEquals(btn, activeNavButton) ? "SidebarButtonActiveStyle" : "SidebarButtonStyle");
            }
        }
    }

    #endregion

    #region Data loading

    /// <summary>Reloads all grids and combo box sources from SQLite.</summary>
    private void LoadAllData()
    {
        LoadClients();
        LoadCases();
        LoadDocuments();
        LoadNotes();
        BindCaseCombos();
    }

    private void LoadClients()
    {
        _clients.Clear();
        foreach (var client in DatabaseHelper.GetAllClients())
        {
            _clients.Add(client);
        }
    }

    private void LoadCases(string? search = null)
    {
        _suppressCaseSearch = true;
        var results = string.IsNullOrWhiteSpace(search)
            ? DatabaseHelper.GetAllCases()
            : DatabaseHelper.SearchCases(search);

        _cases.Clear();
        foreach (var item in results)
        {
            _cases.Add(item);
        }

        BindCaseCombos();
        _suppressCaseSearch = false;
    }

    private void LoadDocuments()
    {
        _documents.Clear();
        foreach (var doc in DatabaseHelper.GetAllDocuments())
        {
            _documents.Add(doc);
        }
    }

    private void LoadNotes()
    {
        _notes.Clear();
        foreach (var note in DatabaseHelper.GetAllCaseNotes())
        {
            _notes.Add(note);
        }
    }

    /// <summary>Populates case dropdowns used on Cases, Documents, and Notes forms.</summary>
    private void BindCaseCombos()
    {
        var caseList = DatabaseHelper.GetAllCases();
        CaseClientCombo.ItemsSource = DatabaseHelper.GetAllClients();

        DocCaseCombo.ItemsSource = caseList;
        NoteCaseCombo.ItemsSource = caseList;
    }

    #endregion

    #region Cases

    private void CaseSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressCaseSearch)
        {
            return;
        }

        LoadCases(CaseSearchBox.Text);
    }

    private void RefreshCases_Click(object sender, RoutedEventArgs e)
    {
        CaseSearchBox.Clear();
        LoadCases();
    }

    private void CasesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CasesGrid.SelectedItem is not CaseRecord selected)
        {
            return;
        }

        _editingCase = selected;
        CaseNameBox.Text = selected.CaseName;
        CaseCourtBox.Text = selected.CourtName;
        CaseNumberBox.Text = selected.CaseNumber;
        CaseNotesBox.Text = selected.Notes;
        CaseOpeningPicker.SelectedDate = selected.OpeningDate;

        CaseStatusCombo.SelectedIndex = selected.Status switch
        {
            "Closed" => 1,
            "Pending" => 2,
            _ => 0
        };

        if (selected.ClientID.HasValue)
        {
            CaseClientCombo.SelectedValue = selected.ClientID;
            foreach (ClientRecord client in CaseClientCombo.Items)
            {
                if (client.ClientID == selected.ClientID)
                {
                    CaseClientCombo.SelectedItem = client;
                    break;
                }
            }
        }
        else
        {
            CaseClientCombo.SelectedIndex = -1;
        }
    }

    private void NewCase_Click(object sender, RoutedEventArgs e)
    {
        _editingCase = null;
        CasesGrid.SelectedItem = null;
        CaseNameBox.Clear();
        CaseCourtBox.Clear();
        CaseNumberBox.Clear();
        CaseNotesBox.Clear();
        CaseOpeningPicker.SelectedDate = DateTime.Today;
        CaseStatusCombo.SelectedIndex = 0;
        CaseClientCombo.SelectedIndex = -1;
        CaseNameBox.Focus();
    }

    private void SaveCase_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CaseNameBox.Text))
        {
            MessageBox.Show(this, "Case name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var record = BuildCaseFromForm();
        try
        {
            if (_editingCase == null)
            {
                record.CaseID = DatabaseHelper.InsertCase(record);
                MessageBox.Show(this, "Case created.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                record.CaseID = _editingCase.CaseID;
                DatabaseHelper.UpdateCase(record);
                MessageBox.Show(this, "Case updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            LoadCases(CaseSearchBox.Text);
            _editingCase = record;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save case:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteCase_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCase == null)
        {
            MessageBox.Show(this, "Select a case to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, $"Delete case \"{_editingCase.CaseName}\"?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        DatabaseHelper.DeleteCase(_editingCase.CaseID);
        NewCase_Click(sender, e);
        LoadCases(CaseSearchBox.Text);
    }

    private CaseRecord BuildCaseFromForm()
    {
        int? clientId = null;
        if (CaseClientCombo.SelectedItem is ClientRecord client)
        {
            clientId = client.ClientID;
        }

        var statusItem = CaseStatusCombo.SelectedItem as ComboBoxItem;
        var status = statusItem?.Content?.ToString() ?? "Active";

        return new CaseRecord
        {
            CaseName = CaseNameBox.Text.Trim(),
            ClientID = clientId,
            CourtName = CaseCourtBox.Text.Trim(),
            CaseNumber = CaseNumberBox.Text.Trim(),
            OpeningDate = CaseOpeningPicker.SelectedDate,
            Status = status,
            Notes = CaseNotesBox.Text.Trim()
        };
    }

    #endregion

    #region Clients

    private void ClientsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClientsGrid.SelectedItem is not ClientRecord selected)
        {
            return;
        }

        _editingClient = selected;
        ClientNameBox.Text = selected.Name;
        ClientPhoneBox.Text = selected.Phone;
        ClientEmailBox.Text = selected.Email;
        ClientAddressBox.Text = selected.Address;
    }

    private void SaveClient_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ClientNameBox.Text))
        {
            MessageBox.Show(this, "Client name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var record = new ClientRecord
        {
            Name = ClientNameBox.Text.Trim(),
            Phone = ClientPhoneBox.Text.Trim(),
            Email = ClientEmailBox.Text.Trim(),
            Address = ClientAddressBox.Text.Trim()
        };

        record.ClientID = DatabaseHelper.InsertClient(record);
        LoadClients();
        BindCaseCombos();
        ClearClientForm_Click(sender, e);
        MessageBox.Show(this, "Client added.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateClient_Click(object sender, RoutedEventArgs e)
    {
        if (_editingClient == null)
        {
            MessageBox.Show(this, "Select a client to update.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientNameBox.Text))
        {
            MessageBox.Show(this, "Client name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _editingClient.Name = ClientNameBox.Text.Trim();
        _editingClient.Phone = ClientPhoneBox.Text.Trim();
        _editingClient.Email = ClientEmailBox.Text.Trim();
        _editingClient.Address = ClientAddressBox.Text.Trim();

        DatabaseHelper.UpdateClient(_editingClient);
        LoadClients();
        LoadCases(CaseSearchBox.Text);
        MessageBox.Show(this, "Client updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteClient_Click(object sender, RoutedEventArgs e)
    {
        if (_editingClient == null)
        {
            MessageBox.Show(this, "Select a client to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, $"Delete client \"{_editingClient.Name}\"?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        DatabaseHelper.DeleteClient(_editingClient.ClientID);
        ClearClientForm_Click(sender, e);
        LoadClients();
        LoadCases(CaseSearchBox.Text);
    }

    private void ClearClientForm_Click(object sender, RoutedEventArgs e)
    {
        _editingClient = null;
        ClientsGrid.SelectedItem = null;
        ClientNameBox.Clear();
        ClientPhoneBox.Clear();
        ClientEmailBox.Clear();
        ClientAddressBox.Clear();
    }

    #endregion

    #region Documents

    private void BrowseDocument_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select document (PDF or any file)",
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            DocPathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(DocNameBox.Text))
            {
                DocNameBox.Text = System.IO.Path.GetFileName(dialog.FileName);
            }
        }
    }

    private void SaveDocument_Click(object sender, RoutedEventArgs e)
    {
        if (DocCaseCombo.SelectedItem is not CaseRecord caseRecord)
        {
            MessageBox.Show(this, "Select a case.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(DocNameBox.Text) || string.IsNullOrWhiteSpace(DocPathBox.Text))
        {
            MessageBox.Show(this, "Document name and file path are required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var typeItem = DocTypeCombo.SelectedItem as ComboBoxItem;
        // Simulated upload: copy file beside the app and persist the local path in SQLite.
        var uploadsDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LawyerCaseManager", "UploadedDocuments");
        System.IO.Directory.CreateDirectory(uploadsDir);
        var storedName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{System.IO.Path.GetFileName(DocPathBox.Text)}";
        var storedPath = System.IO.Path.Combine(uploadsDir, storedName);
        System.IO.File.Copy(DocPathBox.Text.Trim(), storedPath, overwrite: true);

        var doc = new DocumentRecord
        {
            CaseID = caseRecord.CaseID,
            DocName = DocNameBox.Text.Trim(),
            FilePath = storedPath,
            UploadDate = DateTime.Now,
            DocType = typeItem?.Content?.ToString() ?? "Contract"
        };

        DatabaseHelper.InsertDocument(doc);
        LoadDocuments();
        DocNameBox.Clear();
        DocPathBox.Clear();
        MessageBox.Show(this, "Document registered (path saved to database).", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteDocument_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentsGrid.SelectedItem is not DocumentRecord doc)
        {
            MessageBox.Show(this, "Select a document to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, $"Remove document \"{doc.DocName}\" from the database?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        DatabaseHelper.DeleteDocument(doc.DocID);
        LoadDocuments();
    }

    #endregion

    #region Notes timeline

    private void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        if (NoteCaseCombo.SelectedItem is not CaseRecord caseRecord)
        {
            MessageBox.Show(this, "Select a case.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(NoteTextBox.Text))
        {
            MessageBox.Show(this, "Note text is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var note = new CaseNoteRecord
        {
            CaseID = caseRecord.CaseID,
            NoteText = NoteTextBox.Text.Trim(),
            CreatedDate = DateTime.Now,
            Author = string.IsNullOrWhiteSpace(NoteAuthorBox.Text) ? "Staff" : NoteAuthorBox.Text.Trim()
        };

        DatabaseHelper.InsertCaseNote(note);
        LoadNotes();
        NoteTextBox.Clear();
        MessageBox.Show(this, "Note added to timeline.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is not CaseNoteRecord note)
        {
            MessageBox.Show(this, "Select a note to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, "Delete this note?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        DatabaseHelper.DeleteCaseNote(note.NoteID);
        LoadNotes();
    }

    #endregion

    #region Calendar

    private void CourtCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshCalendarDay();
    }

    /// <summary>Shows cases whose opening date matches the calendar selection (court date / deadline proxy).</summary>
    private void RefreshCalendarDay()
    {
        var selected = CourtCalendar.SelectedDate ?? DateTime.Today;
        CalendarDateLabel.Text = $"Cases on {selected:D}";

        _calendarCases.Clear();
        foreach (var item in DatabaseHelper.GetCasesByDate(selected))
        {
            _calendarCases.Add(item);
        }

        // Visual hint: bold title when the day has cases in the database
        var datesWithCases = DatabaseHelper.GetDatesWithCases();
        if (datesWithCases.Contains(selected.Date))
        {
            CalendarDateLabel.Text += " — scheduled opening / deadline";
        }
    }

    #endregion
}
