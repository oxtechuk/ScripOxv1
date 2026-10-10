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

        // Global Exception Handlers to prevent silent app crashes
        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception caught");
            var inner = args.Exception.InnerException != null ? $"\n\nالتفاصيل:\n{args.Exception.InnerException.Message}" : "";
            MessageBox.Show($"حدث خطأ في النظام:\n{args.Exception.Message}{inner}", "ScripOx CRM", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Fatal AppDomain exception");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        // Bypass login for development
        Services.AuthService.SetDevelopmentUser();

        // Initialize SQLite DB if needed
        Services.DatabaseInitializer.Initialize(AppSettings.ConnectionString);

        // Load user language preference
        Services.LocalizationService.Initialize(Services.AuthService.CurrentUser!.Language);

        // Auto-start Python Backend
        StartPythonBackend();

        base.OnStartup(e);

        if (!Services.LicenseService.IsActivated())
        {
            Log.Information("App is not activated. Showing ActivationView.");
            var activationView = new Views.Auth.ActivationView();
            activationView.Show();
        }
        else
        {
            Log.Information("App is activated. Showing DashboardView.");
            var dashboardView = new Views.Dashboard.DashboardView();
            dashboardView.Show();
        }
    }

    public void RestartPythonBackend()
    {
        Log.Information("Restarting Python backend...");
        if (_pythonProcess is not null && !_pythonProcess.HasExited)
        {
            try { _pythonProcess.Kill(); } catch { }
        }
        StartPythonBackend();
    }

    private void StartPythonBackend()
    {
        var port = AppSettings.ApiPort;
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Production check: Bundled standalone executable
            var prodExe1 = Path.Combine(baseDir, "extractor", "ScripOxBackend.exe");
            var prodExe2 = Path.Combine(baseDir, "ScripOxBackend.exe");

            if (File.Exists(prodExe1) || File.Exists(prodExe2))
            {
                var exePath = File.Exists(prodExe1) ? prodExe1 : prodExe2;
                var workingDir = Path.GetDirectoryName(exePath) ?? baseDir;

                Log.Information("Starting production Python backend: {Path} on port {Port}", exePath, port);
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--port {port}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _pythonProcess = Process.Start(startInfo);
                return;
            }

            // 2. Production check: Bundled virtualenv in extractor/.venv
            var bundledPythonVenv = Path.Combine(baseDir, "extractor", ".venv", "Scripts", "python.exe");
            var bundledMain = Path.Combine(baseDir, "extractor", "main.py");
            if (File.Exists(bundledPythonVenv) && File.Exists(bundledMain))
            {
                var extractorDir = Path.Combine(baseDir, "extractor");
                Log.Information("Starting bundled virtualenv Python backend: {Path} on port {Port}", bundledPythonVenv, port);
                var startInfo = new ProcessStartInfo
                {
                    FileName = bundledPythonVenv,
                    Arguments = $"-m uvicorn main:app --port {port}",
                    WorkingDirectory = extractorDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _pythonProcess = Process.Start(startInfo);
                return;
            }

            // 3. Production check: Embedded Python in extractor/python
            var embeddedPython = Path.Combine(baseDir, "extractor", "python", "python.exe");
            if (File.Exists(embeddedPython) && File.Exists(bundledMain))
            {
                var extractorDir = Path.Combine(baseDir, "extractor");
                Log.Information("Starting embedded Python backend: {Path} on port {Port}", embeddedPython, port);
                var startInfo = new ProcessStartInfo
                {
                    FileName = embeddedPython,
                    Arguments = $"-m uvicorn main:app --port {port}",
                    WorkingDirectory = extractorDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _pythonProcess = Process.Start(startInfo);
                return;
            }

            // 4. Production check: System Python with bundled extractor/main.py
            if (File.Exists(bundledMain))
            {
                var extractorDir = Path.Combine(baseDir, "extractor");
                Log.Information("Starting Python backend with system python from: {Path} on port {Port}", extractorDir, port);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python.exe",
                    Arguments = $"-m uvicorn main:app --port {port}",
                    WorkingDirectory = extractorDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _pythonProcess = Process.Start(startInfo);
                return;
            }

            // 5. Development check: Local source repo with virtualenv
            var devExtractorDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\ScripOx.Extractor"));
            if (!Directory.Exists(devExtractorDir))
            {
                devExtractorDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\ScripOx.Extractor"));
            }
            var devPythonExe = Path.Combine(devExtractorDir, @".venv\Scripts\python.exe");

            if (Directory.Exists(devExtractorDir) && File.Exists(devPythonExe))
            {
                Log.Information("Starting development Python backend (FastAPI) at port {Port}...", port);
                var startInfo = new ProcessStartInfo
                {
                    FileName = devPythonExe,
                    Arguments = $"-m uvicorn main:app --port {port}",
                    WorkingDirectory = devExtractorDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _pythonProcess = Process.Start(startInfo);
                return;
            }

            Log.Warning("Could not find production or development ScripOx backend. Extraction API will not start automatically.");
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
