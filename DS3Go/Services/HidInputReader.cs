using HidSharp;
using DS3Go.Models;
using DS3Go.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DS3Go.Services;

/// <summary>
/// Reads DS3 input directly via HID (HidSharp), bypassing XInput entirely.
/// Required for DsHidMini (SXS mode) + HidHide to work:
///   - HidHide hides the DS3 HID device from games
///   - DS3Go.exe is whitelisted in HidHide → HidSharp can still open the device
///   - DS3Go creates ViGEm virtual controllers → games only see those
/// </summary>
public sealed class HidInputReader : IInputReader
{
    private const int DS3_VID = 0x054C;
    private const int DS3_PID = 0x0268;
    private const int ReadTimeoutMs = 20;

    private readonly ILogger<HidInputReader> _logger;
    private readonly ControllerInput[] _states = new ControllerInput[4];
    private readonly HidStream?[] _streams = new HidStream?[4];
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public event Action<int, ControllerInput>? InputUpdated;

    public HidInputReader(ILogger<HidInputReader> logger)
    {
        _logger = logger;
        for (int i = 0; i < 4; i++)
            _states[i] = new ControllerInput();
    }

    public void StartReading()
    {
        if (_cts != null) return;

        ScanDevices();

        _cts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoop(_cts.Token));
        _logger.LogInformation("Lectura HID DS3 iniciada.");
    }

    /// <summary>
    /// Scans for DS3 HID devices and opens streams.
    /// Can be called again to rescan (e.g., after a device is connected).
    /// </summary>
    public void ScanDevices()
    {
        lock (_lock)
        {
            // Close any existing streams
            for (int i = 0; i < 4; i++)
            {
                _streams[i]?.Close();
                _streams[i] = null;
                _states[i].IsConnected = false;
            }

            try
            {
                var devices = DeviceList.Local
                    .GetHidDevices(vendorID: DS3_VID, productID: DS3_PID)
                    .ToArray();

                _logger.LogInformation("HID scan: {Count} DS3(s) encontrado(s).", devices.Length);

                for (int i = 0; i < Math.Min(devices.Length, 4); i++)
                {
                    try
                    {
                        var config = new OpenConfiguration();
                        config.SetOption(OpenOption.Exclusive, false);
                        config.SetOption(OpenOption.Interruptible, true);

                        var stream = devices[i].Open(config);
                        stream.ReadTimeout = ReadTimeoutMs;
                        _streams[i] = stream;
                        _states[i].IsConnected = true;
                        _logger.LogInformation("DS3 HID[{Index}] abierto: {Path}",
                            i, devices[i].DevicePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo abrir DS3 HID[{Index}]: {Msg}", i, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al escanear dispositivos HID.");
            }
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < 4; i++)
            {
                var stream = _streams[i];
                if (stream == null)
                {
                    _states[i].IsConnected = false;
                    InputUpdated?.Invoke(i, _states[i]);
                    continue;
                }

                try
                {
                    var report = stream.Read();
                    ParseDS3Report(report, _states[i]);
                    _states[i].IsConnected = true;
                }
                catch (TimeoutException)
                {
                    // No report available this cycle — controller is idle, still connected
                }
                catch (Exception)
                {
                    // Device disconnected or error
                    _states[i].IsConnected = false;
                    try { stream.Close(); } catch { }
                    _streams[i] = null;
                }

                InputUpdated?.Invoke(i, _states[i]);
            }

            Thread.Sleep(4); // ~250Hz poll for low-latency
        }
    }

    /// <summary>
    /// Parses a raw DS3 HID report (DsHidMini SXS mode / standard SixAxis format).
    /// On Windows, HidSharp includes the Report ID as byte[0].
    /// </summary>
    private static void ParseDS3Report(byte[] r, ControllerInput input)
    {
        // Minimum valid DS3 report is ~27 bytes (buttons + sticks + pressure)
        // Full report is 49 bytes. We only need the first 26 data bytes.
        if (r.Length < 10) return;

        // byte[0] = Report ID (0x01)
        // byte[1] = Reserved
        // byte[2] = Buttons group 1
        // byte[3] = Buttons group 2
        // byte[4] = PS button + reserved
        // byte[5] = Reserved
        // byte[6..9] = Analog sticks

        byte b1 = r.Length > 2 ? r[2] : (byte)0;
        byte b2 = r.Length > 3 ? r[3] : (byte)0;
        byte ps = r.Length > 4 ? r[4] : (byte)0;

        // Buttons group 1
        input.Buttons[DS3Button.Select]    = (b1 & 0x01) != 0;
        input.Buttons[DS3Button.L3]        = (b1 & 0x02) != 0;
        input.Buttons[DS3Button.R3]        = (b1 & 0x04) != 0;
        input.Buttons[DS3Button.Start]     = (b1 & 0x08) != 0;
        input.Buttons[DS3Button.DPadUp]    = (b1 & 0x10) != 0;
        input.Buttons[DS3Button.DPadRight] = (b1 & 0x20) != 0;
        input.Buttons[DS3Button.DPadDown]  = (b1 & 0x40) != 0;
        input.Buttons[DS3Button.DPadLeft]  = (b1 & 0x80) != 0;

        // Buttons group 2
        input.Buttons[DS3Button.L2]       = (b2 & 0x01) != 0;
        input.Buttons[DS3Button.R2]       = (b2 & 0x02) != 0;
        input.Buttons[DS3Button.L1]       = (b2 & 0x04) != 0;
        input.Buttons[DS3Button.R1]       = (b2 & 0x08) != 0;
        input.Buttons[DS3Button.Triangle] = (b2 & 0x10) != 0;
        input.Buttons[DS3Button.Circle]   = (b2 & 0x20) != 0;
        input.Buttons[DS3Button.Cross]    = (b2 & 0x40) != 0;
        input.Buttons[DS3Button.Square]   = (b2 & 0x80) != 0;

        // PS button
        input.Buttons[DS3Button.PS] = (ps & 0x01) != 0;

        // Analog sticks (0=left/up, 128=center, 255=right/down)
        if (r.Length > 9)
        {
            input.LeftStickX  = Normalize(r[6]);
            input.LeftStickY  = -Normalize(r[7]); // Invert Y
            input.RightStickX = Normalize(r[8]);
            input.RightStickY = -Normalize(r[9]); // Invert Y
        }

        // Pressure axes (bytes 18-19 for L2/R2)
        if (r.Length > 19)
        {
            input.L2Pressure = r[18] / 255f;
            input.R2Pressure = r[19] / 255f;
        }
    }

    /// <summary>
    /// Normalizes a 0-255 stick value to -1.0..1.0 with 0.05 deadzone.
    /// </summary>
    private static float Normalize(byte raw)
    {
        float v = (raw - 128) / 128f;
        return Math.Abs(v) < 0.05f ? 0f : Math.Clamp(v, -1f, 1f);
    }

    public ControllerInput? GetCurrentState(int index)
    {
        return index >= 0 && index < 4 ? _states[index] : null;
    }

    public bool IsControllerConnected(int index)
    {
        return index >= 0 && index < 4 && _streams[index] != null;
    }

    public void StopReading()
    {
        _cts?.Cancel();
        try { _readTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
        _cts = null;

        for (int i = 0; i < 4; i++)
        {
            try { _streams[i]?.Close(); } catch { }
            _streams[i] = null;
        }
        _logger.LogInformation("Lectura HID DS3 detenida.");
    }

    public void Dispose() => StopReading();
}
