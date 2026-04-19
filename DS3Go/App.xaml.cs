using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using DS3Go.Services;
using DS3Go.Services.Interfaces;
using DS3Go.ViewModels;

namespace DS3Go;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DS3Go", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "ds3go-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.AddSingleton<IPersistenceService, JsonPersistenceService>();
        services.AddSingleton<IRemappingEngine, RemappingEngine>();
        services.AddSingleton<IInputReader, XInputReader>();
        services.AddSingleton<IVirtualControllerService, ViGEmControllerService>();

        services.AddSingleton<IDeviceIdentifier>(sp =>
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "known_devices.json");
            var known = new KnownDeviceIdentifier(dbPath, sp.GetRequiredService<ILogger<KnownDeviceIdentifier>>());
            var heuristic = new HeuristicDeviceIdentifier(sp.GetRequiredService<ILogger<HeuristicDeviceIdentifier>>());
            return new DeviceIdentifierChain(
                new IDeviceIdentifier[] { known, heuristic },
                sp.GetRequiredService<ILogger<DeviceIdentifierChain>>());
        });

        services.AddSingleton<IDeviceDetector, UsbDeviceDetector>();
        services.AddSingleton<IPortManager, PortManager>();
        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            _serviceProvider.GetService<IPortManager>()?.SaveState();
            _serviceProvider.GetService<IVirtualControllerService>()?.Dispose();
            _serviceProvider.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
