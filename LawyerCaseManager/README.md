# Lawyer Case Manager

A **100% offline** Windows desktop application for managing legal cases, clients, documents, notes, and a calendar view of court dates. Built with **.NET 8 WPF**, **SQLite** (`Microsoft.Data.Sqlite`), and Material-inspired styling (navy, white, gray, Segoe UI).

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (includes WPF / Windows Desktop runtime)

### First-time setup (PATH)

If `dotnet` is not recognized, run from this folder:

```powershell
.\setup-environment.ps1
```

Or install manually:

```powershell
winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
```

**Paths configured (user environment):**

| Variable / PATH | Location |
|-----------------|----------|
| `dotnet` on PATH | `C:\Program Files\dotnet` |
| `DOTNET_ROOT` | `C:\Program Files\dotnet` |

Restart Cursor or open a **new** terminal after setup so PATH updates apply.

## Build

```powershell
cd C:\Users\rashi\Projects\LawyerCaseManager
dotnet restore
dotnet build
```

## Run

```powershell
dotnet run
```

Or run the executable after build:

`bin\Debug\net8.0-windows\LawyerCaseManager.exe`

## Database

SQLite file location (created automatically on first run):

`%LOCALAPPDATA%\LawyerCaseManager\lawyers_app.db`

All SQL uses parameterized queries via `DatabaseHelper.cs`.

## Features

| Section | Description |
|--------|-------------|
| **Cases** | List with instant search; create/edit/delete cases with full schema fields |
| **Clients** | Add, update, delete, and list clients |
| **Documents** | Browse for a file (e.g. PDF); path and metadata stored in SQLite |
| **Notes Timeline** | Chronological notes (newest first) with author and case |
| **Calendar** | WPF `Calendar` control; select a day to see cases with that opening date |

## Project layout

- `DatabaseHelper.cs` — schema initialization and CRUD
- `MainWindow.xaml` / `MainWindow.xaml.cs` — UI and code-behind
- `App.xaml` / `App.xaml.cs` — application resources and startup
- `Models/` — data transfer records

## License

Private use — created for local case management without network dependency.
