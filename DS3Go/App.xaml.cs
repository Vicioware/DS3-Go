using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using DS3Go.Services;
using DS3Go.Services.Interfaces;
using DS3Go.ViewModels;
using HidSharp;

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
        services.AddSingleton<IVirtualControllerService, ViGEmControllerService>();

        // Input reader: try HID (DsHidMini SXS) first, fall back to XInput (SCP/XInput mode)
        services.AddSingleton<IInputReader>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>();

            // Check if any DS3 HID device is accessible
            bool hasHidDs3 = false;
            try
            {
                hasHidDs3 = DeviceList.Local
                    .GetHidDevices(vendorID: 0x054C, productID: 0x0268)
                    .Any();
            }
            catch { }

            if (hasHidDs3)
            {
                Log.Logger.Information(
                    "DS3 HID detectado → usando lectura HID directa (DsHidMini SXS).");
                return new HidInputReader(
                    logger.CreateLogger<HidInputReader>());
            }

            Log.Logger.Information(
                "Sin DS3 HID → usando lectura XInput (SCP/DsHidMini XInput mode).");
            return new XInputReader(
                logger.CreateLogger<XInputReader>());
        });

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
