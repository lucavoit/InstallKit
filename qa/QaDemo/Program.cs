// The application the QA installer ships. Every mode leaves evidence the QA tests can assert on.
//   QaDemo.exe --init <text>      run by the installer after install: writes init.txt next to the exe
//   QaDemo.exe --unregister       run by the installer before a real uninstall: writes a marker to ProgramData
//   QaDemo.exe                    as the Windows service: writes heartbeat.txt every few seconds

const string ServiceName = "InstallKitQaDemo";
var appDir = AppContext.BaseDirectory;
var evidenceDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InstallKitQa");

if (args.Length > 0 && args[0] == "--init")
{
    File.WriteAllText(Path.Combine(appDir, "init.txt"), string.Join(' ', args.Skip(1)));
    return 0;
}

if (args.Length > 0 && args[0] == "--unregister")
{
    Directory.CreateDirectory(evidenceDir);
    File.WriteAllText(Path.Combine(evidenceDir, "unregistered.txt"), DateTimeOffset.Now.ToString("O"));
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = ServiceName);
builder.Services.AddHostedService(_ => new Heartbeat(Path.Combine(appDir, "heartbeat.txt")));
builder.Build().Run();
return 0;

sealed class Heartbeat(string file) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            File.WriteAllText(file, DateTimeOffset.Now.ToString("O"));
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
