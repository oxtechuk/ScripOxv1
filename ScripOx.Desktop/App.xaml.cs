using System.Windows;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace ScripOx.Desktop;

public partial class App : Application
{
    private Process? _pythonProcess;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: "logs/scripox_.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("ScripOx Desktop starting — Ox Tech (oxtech.uk)");

        // Bypass login for development
        Services.AuthService.SetDevelopmentUser();

        // Initialize SQLite DB if needed
        Services.DatabaseInitializer.Initialize(AppSettings.ConnectionString);

        // Load user language preference
        Services.LocalizationService.Initialize(Services.AuthService.CurrentUser!.Language);

        // Auto-start Python Backend
        StartPythonBackend();

        base.OnStartup(e);
    }

    private void StartPythonBackend()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var extractorDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\ScripOx.Extractor"));
            var activateScript = Path.Combine(extractorDir, @".venv\Scripts\activate.bat");

            if (Directory.Exists(extractorDir))
            {
                Log.Information("Starting Python Backend (FastAPI) at port 8765...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{activateScript}\" && uvicorn main:app --port 8765",
                    WorkingDirectory = extractorDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                _pythonProcess = Process.Start(startInfo);
            }
            else
            {
                Log.Warning("Could not find ScripOx.Extractor directory. Backend will not start automatically.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start Python backend automatically.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("ScripOx Desktop shutting down.");
        
        if (_pythonProcess is not null && !_pythonProcess.HasExited)
        {
            try { _pythonProcess.Kill(); } catch { }
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
