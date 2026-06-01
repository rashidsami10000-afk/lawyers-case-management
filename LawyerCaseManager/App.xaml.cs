using System.Windows;

namespace LawyerCaseManager;

/// <summary>
/// WPF application entry point. Initializes SQLite schema before showing the main window.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DatabaseHelper.Initialize();
    }
}
