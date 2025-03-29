using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using Godot.Collections;
public partial class DeviceManager : Node
{
    // Signals
    [Signal]
    public delegate void DeviceInfoUpdatedEventHandler();

    [Signal]
    public delegate void NetworkStatusChangedEventHandler(Dictionary statusInfo);

    [Signal]
    public delegate void ScreenOrientationChangedEventHandler(int orientation);

    [Signal]
    public delegate void StorageWarningEventHandler(long availableSpace);

    // Constants
    private const long STORAGE_WARNING_THRESHOLD = 1024 * 1024 * 100; // 100 MB

    // Enums
    public enum NetworkType
    {
        None,
        Ethernet,
        Wifi,
        Cellular,
        Other
    }

    public enum PowerSource
    {
        Battery,
        AcPower,
        Unknown
    }

    // Properties - Basic device information
    public string DeviceId { get; private set; } = string.Empty;
    public string DeviceUniqueId { get; private set; } = string.Empty;
    public string DeviceName { get; private set; } = string.Empty;
    public string DeviceModel { get; private set; } = string.Empty;
    public string DeviceOsName { get; private set; } = string.Empty;
    public string DeviceOsVersion { get; private set; } = string.Empty;
    public bool IsRooted { get; private set; } = false;
    public bool IsEmulator { get; private set; } = false;

    // Hardware information
    public string ProcessorName { get; private set; } = string.Empty;
    public long StorageTotal { get; private set; } = 0; // In bytes
    public long StorageAvailable { get; private set; } = 0; // In bytes

    // Network information
    public NetworkType NetworkStatus { get; private set; } = NetworkType.None;
    public string IpAddress { get; private set; } = string.Empty;
    public string MacAddress { get; private set; } = string.Empty;

    // Screen information
    public float ScreenDpi { get; private set; } = 0.0f;
    public Vector2I ScreenSize { get; private set; }
    public int ScreenOrientation { get; private set; } = (int)DisplayServer.ScreenOrientation.Landscape;
    public bool IsTouchscreen { get; private set; } = false;
    public float ScreenScale { get; private set; } = 1.0f;

    // Private variables
    private Logger _logger;
    private UIManager _uiManager;
    private bool _initialized = false;

    // Lifecycle Methods
    public override void _Ready()
    {
        // Get references to required managers
        _logger = GetNode<Logger>("/root/Logger");
        _uiManager = GetNode<UIManager>("/root/UiManager");

        GD.Print(_logger);
        _logger.Info("DeviceManager: Initializing...");

        // Connect to UI Manager signals if available
        if (_uiManager != null)
        {
            _uiManager.Connect(UIManager.SignalName.ScreenDataUpdated, new Callable(this, MethodName.OnScreenDataUpdated));
        }

        // Gather initial device information
        GatherDeviceInfo();

        // Mark as initialized
        _initialized = true;

        GD.Print("DeviceManager: Initialized successfully");
    }

    // Public Methods
    /// <summary>
    /// Gathers all available device information and updates properties.
    /// Call this to manually refresh device data.
    /// </summary>
    public void GatherDeviceInfo()
    {
        // Basic device information
        DeviceName = OS.GetName();
        DeviceModel = OS.GetModelName();
        DeviceOsName = OS.GetName();
        DeviceOsVersion = OS.GetVersion();
        DeviceUniqueId = OS.GetUniqueId();

        // If unique ID is empty (which happens on web platforms), create a fallback
        if (string.IsNullOrEmpty(DeviceUniqueId))
        {
            DeviceUniqueId = GenerateDeviceId();
        }

        // Set the device ID to match unique ID or create one
        if (string.IsNullOrEmpty(DeviceId))
        {
            DeviceId = DeviceUniqueId;
        }

        // Hardware information
        ProcessorName = OS.GetProcessorName();

        // Try to detect if device is rooted (basic check)
        IsRooted = CheckIfRooted();
        IsEmulator = CheckIfEmulator();

        // Update screen information
        UpdateScreenInfo();

        // Update network information
        UpdateNetworkInfo();

        // Update storage information
        UpdateStorageInfo();

        // Emit signal for listeners
        EmitSignal(SignalName.DeviceInfoUpdated);
    }

    /// <summary>
    /// Returns a Dictionary with all device information.
    /// Helpful for diagnostics and sending device info to the server.
    /// </summary>
    public Godot.Collections.Dictionary GetDeviceInfo()
    {
        var basicInfo = new Godot.Collections.Dictionary
        {
            { "device_id", DeviceId },
            { "device_unique_id", DeviceUniqueId },
            { "device_name", DeviceName },
            { "device_model", DeviceModel },
            { "device_os_name", DeviceOsName },
            { "device_os_version", DeviceOsVersion },
            { "is_rooted", IsRooted },
            { "is_emulator", IsEmulator }
        };

        var hardwareInfo = new Godot.Collections.Dictionary
        {
            { "processor_name", ProcessorName },
            { "storage_total", StorageTotal },
            { "storage_available", StorageAvailable }
        };

        var networkInfo = new Godot.Collections.Dictionary
        {
            { "network_status", (int)NetworkStatus },
            { "ip_address", IpAddress },
            { "mac_address", MacAddress }
        };

        var screenSizeInfo = new Godot.Collections.Dictionary
        {
            { "width", ScreenSize.X },
            { "height", ScreenSize.Y }
        };

        var screenInfo = new Godot.Collections.Dictionary
        {
            { "screen_dpi", ScreenDpi },
            { "screen_size", screenSizeInfo },
            { "screen_orientation", ScreenOrientation },
            { "is_touchscreen", IsTouchscreen },
            { "screen_scale", ScreenScale }
        };

        var result = new Godot.Collections.Dictionary
        {
            { "basic_info", basicInfo },
            { "hardware_info", hardwareInfo },
            { "network_info", networkInfo },
            { "screen_info", screenInfo }
        };

        if (_uiManager != null)
        {
            result.Add("ui_info", _uiManager.GetInfoAsDictionary());
        }

        return result;
    }

    /// <summary>
    /// Check if the device is low on storage space
    /// </summary>
    public bool IsStorageLow()
    {
        return StorageAvailable < STORAGE_WARNING_THRESHOLD;
    }

    /// <summary>
    /// Check if the network is connected
    /// </summary>
    public bool IsNetworkConnected()
    {
        return NetworkStatus != NetworkType.None;
    }

    /// <summary>
    /// Get the network connection type as a string
    /// </summary>
    public string GetNetworkTypeString()
    {
        return NetworkStatus switch
        {
            NetworkType.None => "None",
            NetworkType.Ethernet => "Ethernet",
            NetworkType.Wifi => "WiFi",
            NetworkType.Cellular => "Cellular",
            NetworkType.Other => "Other",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Run diagnostics on the device and return a comprehensive report
    /// </summary>
    public string RunDiagnostics()
    {
        var diagnostics = "=== DEVICE MANAGER DIAGNOSTICS ===\n";
        diagnostics += $"Current Time (UTC): {Time.GetDatetimeStringFromSystem(true)}\n";

        // Check system
        diagnostics += "\n--- SYSTEM INFORMATION ---\n";
        diagnostics += $"Device ID: {DeviceId}\n";
        diagnostics += $"Device Model: {DeviceModel}\n";
        diagnostics += $"OS Name: {DeviceOsName}\n";
        diagnostics += $"OS Version: {DeviceOsVersion}\n";
        diagnostics += $"Processor: {ProcessorName}\n";
        diagnostics += $"Rooted/Jailbroken: {(IsRooted ? "Yes" : "No")}\n";
        diagnostics += $"Emulator: {(IsEmulator ? "Yes" : "No")}\n";

        // Check storage
        diagnostics += "\n--- STORAGE ---\n";
        var storageTotalGb = (float)StorageTotal / (1024 * 1024 * 1024);
        var storageAvailableGb = (float)StorageAvailable / (1024 * 1024 * 1024);
        var storageUsedGb = ((float)StorageTotal - StorageAvailable) / (1024 * 1024 * 1024);
        var storagePercent = StorageTotal == 0 ? 0.0f : (float)(StorageTotal - StorageAvailable) / (float)StorageTotal * 100.0f;

        diagnostics += $"Total: {storageTotalGb:F2} GB\n";
        diagnostics += $"Available: {storageAvailableGb:F2} GB\n";
        diagnostics += $"Used: {storageUsedGb:F2} GB ({storagePercent:F1}%)\n";
        diagnostics += $"Storage Warning: {(IsStorageLow() ? "Yes" : "No")}\n";

        // Check network
        diagnostics += "\n--- NETWORK ---\n";
        diagnostics += $"Status: {GetNetworkTypeString()}\n";
        diagnostics += $"IP Address: {IpAddress}\n";
        diagnostics += $"MAC Address: {MacAddress}\n";

        // Check screen
        diagnostics += "\n--- DISPLAY ---\n";
        diagnostics += $"Size: {ScreenSize.X}x{ScreenSize.Y}\n";
        diagnostics += $"DPI: {ScreenDpi:F1}\n";
        diagnostics += $"Scale: {ScreenScale:F2}\n";
        diagnostics += $"Orientation: {GetOrientationString(ScreenOrientation)}\n";
        diagnostics += $"Touchscreen: {(IsTouchscreen ? "Yes" : "No")}\n";

        diagnostics += "\n=== DIAGNOSTICS COMPLETE ===\n";
        return diagnostics;
    }

    /// <summary>
    /// Sets the screen orientation (if supported by the platform)
    /// </summary>
    public bool SetScreenOrientation(int orientation)
    {
        if (!IsMobilePlatform())
        {
            return false;
        }

        DisplayServer.ScreenSetOrientation((DisplayServer.ScreenOrientation)orientation);
        ScreenOrientation = (int)DisplayServer.ScreenGetOrientation();
        EmitSignal(SignalName.ScreenOrientationChanged, ScreenOrientation);
        return true;
    }

    /// <summary>
    /// Keep screen on to prevent sleep (for kiosk mode)
    /// </summary>
    public void SetScreenAlwaysOn(bool enable)
    {
        DisplayServer.ScreenSetKeepOn(enable);
    }

    // Private Methods

    /// <summary>
    /// Generate a stable device ID based on hardware info
    /// </summary>
    private string GenerateDeviceId()
    {
        // Combine unique identifiers to create a stable ID
        var baseString = $"{OS.GetName()}_{OS.GetModelName()}_{OS.GetStaticMemoryUsage()}";

        // Create a hash
        return baseString.GetHashCode().ToString().Replace("-", "");
    }

    /// <summary>
    /// Check if device is rooted/jailbroken (basic detection)
    /// </summary>
    private bool CheckIfRooted()
    {
        // Very basic check for rooted devices
        switch (OS.GetName())
        {
            case "Android":
                string[] suPaths =
                [
                    "/system/app/Superuser.apk",
                    "/system/xbin/su",
                    "/system/bin/su",
                    "/sbin/su",
                    "/data/local/su"
                ];

                foreach (var path in suPaths)
                {
                    var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                    if (file != null)
                    {
                        file.Close();
                        return true;
                    }
                }
                break;

            case "iOS":
                var jbPaths = new[]
                {
                    "/Applications/Cydia.app",
                    "/Library/MobileSubstrate/MobileSubstrate.dylib",
                    "/bin/bash",
                    "/usr/sbin/sshd",
                    "/etc/apt"
                };

                foreach (var path in jbPaths)
                {
                    var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                    if (file != null)
                    {
                        file.Close();
                        return true;
                    }
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Check if running on an emulator
    /// </summary>
    private bool CheckIfEmulator()
    {
        switch (OS.GetName())
        {
            case "Android":
                // Check common emulator indicators
                if (DeviceModel.ToLower().Contains("sdk") ||
                    DeviceModel.ToLower().Contains("emulator") ||
                    DeviceModel.ToLower().Contains("android sdk"))
                {
                    return true;
                }

                // Check for typical emulator hardware IDs
                if (DeviceUniqueId.Contains("emulator") || DeviceUniqueId == "000000000000000")
                {
                    return true;
                }
                break;

            case "iOS":
                // Check for simulator indicators (very basic)
                if (DeviceModel.ToLower().Contains("simulator"))
                {
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Update screen information
    /// </summary>
    private void UpdateScreenInfo()
    {
        // Get screen info if UI manager isn't available
        if (_uiManager == null)
        {
            ScreenSize = DisplayServer.ScreenGetSize();
            ScreenDpi = DisplayServer.ScreenGetDpi();
            IsTouchscreen = DisplayServer.IsTouchscreenAvailable();
            ScreenScale = DisplayServer.ScreenGetScale();
            ScreenOrientation = (int)DisplayServer.ScreenGetOrientation();
        }
        else
        {
            // Use UI manager data if available
            var uiInfo = _uiManager.GetInfoAsDictionary();
            ScreenSize = (Vector2I)uiInfo["window_size"];
            IsTouchscreen = (bool)uiInfo["is_touchscreen"];

            // If screens array is populated, get first screen's info
            if (uiInfo.ContainsKey("screens") && ((Godot.Collections.Array)uiInfo["screens"]).Count > 0)
            {
                var screens = (Godot.Collections.Array)uiInfo["screens"];
                if (screens.Count > 0)
                {
                    var firstScreen = (Godot.Collections.Dictionary)screens[0];
                    ScreenDpi = (float)firstScreen["dpi"];
                    ScreenOrientation = (int)firstScreen["orientation"];
                }
            }
        }

        // Fallback for screen scale if not set
        if (ScreenScale <= 0)
        {
            ScreenScale = 1.0f;
        }
    }

    /// <summary>
    /// Update network information
    /// </summary>
    private void UpdateNetworkInfo()
    {
        var oldNetworkStatus = NetworkStatus;
        var oldIpAddress = IpAddress;

        // Get list of IP addresses
        var addresses = IP.GetLocalAddresses();

        // Determine connection type and select an appropriate IP
        NetworkStatus = NetworkType.None;
        IpAddress = string.Empty;

        if (IsMobilePlatform())
        {
            // Mobile platform network detection
            // Check if we have any non-localhost IPs
            foreach (var addr in addresses)
            {
                if (addr != "127.0.0.1" && addr != "::1" && !addr.Contains(':'))
                {
                    IpAddress = addr;
                    NetworkStatus = NetworkType.Wifi;
                    break;
                }
            }
        }
        else
        {
            // Desktop platform network detection
            foreach (var addr in addresses)
            {
                if (addr != "127.0.0.1" && addr != "::1" && !addr.Contains(':'))
                {
                    IpAddress = addr;
                    NetworkStatus = NetworkType.Ethernet;
                    break;
                }
            }
        }

        // If network status changed, emit signal
        if (oldNetworkStatus != NetworkStatus || oldIpAddress != IpAddress)
        {
            var statusInfo = new Godot.Collections.Dictionary
            {
                { "network_status", (int)NetworkStatus },
                { "network_type", GetNetworkTypeString() },
                { "ip_address", IpAddress }
            };

            EmitSignal(SignalName.NetworkStatusChanged, statusInfo);

            if (oldNetworkStatus == NetworkType.None && NetworkStatus != NetworkType.None)
            {
                _logger.Info($"DeviceManager: Network connected: {GetNetworkTypeString()}");
            }
            else if (oldNetworkStatus != NetworkType.None && NetworkStatus == NetworkType.None)
            {
                _logger.Warn("DeviceManager: Network disconnected");
            }
        }
    }

    /// <summary>
    /// Update storage information
    /// </summary>
    private void UpdateStorageInfo()
    {
        // These would be replaced with real platform-specific implementations
        // Simulation for testing - 50GB total storage with 80% used
        StorageTotal = 50L * 1024 * 1024 * 1024; // 50 GB in bytes
        StorageAvailable = 10L * 1024 * 1024 * 1024; // 10 GB in bytes

        // Check if storage is critically low
        if (StorageAvailable < STORAGE_WARNING_THRESHOLD)
        {
            EmitSignal(SignalName.StorageWarning, StorageAvailable);
            _logger.Warn($"DeviceManager: Storage space critically low: {(float)StorageAvailable / 1024.0f / 1024.0f:F2} MB available");
        }
    }

    /// <summary>
    /// Check if the device is running on a mobile platform
    /// </summary>
    private bool IsMobilePlatform()
    {
        string osName = OS.GetName();
        return osName == "Android" || osName == "iOS";
    }

    /// <summary>
    /// Event handler for screen data updates from UIManager
    /// </summary>
    private void OnScreenDataUpdated()
    {
        UpdateScreenInfo();
        _logger.Debug("DeviceManager: Screen information updated");
    }

    /// <summary>
    /// Helper function to get orientation as string
    /// </summary>
    private static string GetOrientationString(int orientation)
    {
        return orientation switch
        {
            (int)DisplayServer.ScreenOrientation.Landscape => "Landscape",
            (int)DisplayServer.ScreenOrientation.Portrait => "Portrait",
            (int)DisplayServer.ScreenOrientation.ReverseLandscape => "Reverse Landscape",
            (int)DisplayServer.ScreenOrientation.ReversePortrait => "Reverse Portrait",
            (int)DisplayServer.ScreenOrientation.SensorLandscape => "Sensor Landscape",
            (int)DisplayServer.ScreenOrientation.SensorPortrait => "Sensor Portrait",
            (int)DisplayServer.ScreenOrientation.Sensor => "Sensor",
            _ => "Unknown"
        };
    }
}
