# DeviceManager.gd
extends Node

# ------------------------------------------------------------------
# Signals
# ------------------------------------------------------------------
## Emitted when device information is updated.
signal device_info_updated
## Emitted when network status changes.
signal network_status_changed(status_info)
## Emitted when screen orientation changes.
signal screen_orientation_changed(orientation)
## Emitted when storage space drops below critical threshold.
signal storage_warning(available_space)

# ------------------------------------------------------------------
# Constants
# ------------------------------------------------------------------
## Critical storage space threshold in bytes.
const STORAGE_WARNING_THRESHOLD := 1024 * 1024 * 100 # 100 MB

# ------------------------------------------------------------------
# Enums
# ------------------------------------------------------------------
## Network connection types.
enum NetworkType {
    NONE,
    ETHERNET,
    WIFI,
    CELLULAR,
    OTHER
}

## Device power sources.
enum PowerSource {
    BATTERY,
    AC_POWER,
    UNKNOWN
}

# ------------------------------------------------------------------
# Properties
# ------------------------------------------------------------------
# Basic device information
var device_id: String
var device_unique_id: String
var device_name: String
var device_model: String
var device_os_name: String
var device_os_version: String
var system_name: String
var is_rooted: bool
var is_emulator: bool

# Hardware information
var processor_name: String
var storage_total: int = 0 # In bytes
var storage_available: int = 0 # In bytes

# Network information
var network_status: int = NetworkType.NONE
var ip_address: String = ""
var mac_address: String = ""

# Screen information
var screen_dpi: float = 0.0
var screen_size: Vector2i
var screen_orientation: int = DisplayServer.SCREEN_LANDSCAPE
var is_touchscreen: bool = false
var screen_scale: float = 1.0

# ------------------------------------------------------------------
# Private variables
# ------------------------------------------------------------------
var _logger: Node
var _ui_manager: Node
var _initialized: bool = false

# ------------------------------------------------------------------
# Lifecycle Methods
# ------------------------------------------------------------------
func _ready() -> void:
    # Get references to required managers
    _logger = get_node_or_null("/root/Logger")
    _ui_manager = get_node_or_null("/root/UIManager")

    # Connect to UI Manager signals if available
    if _ui_manager:
        _ui_manager.connect("screen_data_updated", _on_screen_data_updated)

    # Gather initial device information
    gather_device_info()

    # Mark as initialized
    _initialized = true

    if _logger:
        _logger.call("info", "DeviceManager: Initialized successfully")
    else:
        print("DeviceManager: Initialized successfully")

# ------------------------------------------------------------------
# Public Methods
# ------------------------------------------------------------------
##
# Gathers all available device information and updates properties.
# Call this to manually refresh device data.
##
func gather_device_info() -> void:
    # Basic device information
    device_name = OS.get_name()
    device_model = OS.get_model_name()
    device_os_name = OS.get_name()
    device_os_version = OS.get_version()
    system_name = DisplayServer.get_name()
    device_unique_id = OS.get_unique_id()

    # If unique ID is empty (which happens on web platforms), create a fallback
    if device_unique_id.is_empty():
        device_unique_id = _generate_device_id()

    # Set the device ID to match unique ID or create one
    if device_id.is_empty():
        device_id = device_unique_id

    # Hardware information
    processor_name = OS.get_processor_name()

    # Try to detect if device is rooted (basic check)
    is_rooted = _check_if_rooted()
    is_emulator = _check_if_emulator()

    # Update screen information
    _update_screen_info()

    # Update network information
    _update_network_info()

    # Update storage information
    _update_storage_info()

    # Emit signal for listeners
    emit_signal("device_info_updated")

##
# Returns a Dictionary with all device information.
# Helpful for diagnostics and sending device info to the server.
##
func get_device_info() -> Dictionary:
    return {
        "basic_info": {
            "device_id": device_id,
            "device_unique_id": device_unique_id,
            "device_name": device_name,
            "device_model": device_model,
            "device_os_name": device_os_name,
            "device_os_version": device_os_version,
            "system_name": system_name,
            "is_rooted": is_rooted,
            "is_emulator": is_emulator
        },
        "hardware_info": {
            "processor_name": processor_name,
            "storage_total": storage_total,
            "storage_available": storage_available
        },
        "network_info": {
            "network_status": network_status,
            "ip_address": ip_address,
            "mac_address": mac_address
        },
        "screen_info": {
            "screen_dpi": screen_dpi,
            "screen_size": {
                "width": screen_size.x,
                "height": screen_size.y
            },
            "screen_orientation": screen_orientation,
            "is_touchscreen": is_touchscreen,
            "screen_scale": screen_scale
        },
        "ui_info": _ui_manager.get_info_as_dictionary() if _ui_manager else {}
    }

##
# Check if the device is low on storage space
##
func is_storage_low() -> bool:
    return storage_available < STORAGE_WARNING_THRESHOLD

##
# Check if the network is connected
##
func is_network_connected() -> bool:
    return network_status != NetworkType.NONE

##
# Get the network connection type as a string
##
func get_network_type_string() -> String:
    match network_status:
        NetworkType.NONE:
            return "None"
        NetworkType.ETHERNET:
            return "Ethernet"
        NetworkType.WIFI:
            return "WiFi"
        NetworkType.CELLULAR:
            return "Cellular"
        NetworkType.OTHER:
            return "Other"
    return "Unknown"

##
# Run diagnostics on the device and return a comprehensive report
##
func run_diagnostics() -> String:
    var diagnostics = "=== DEVICE MANAGER DIAGNOSTICS ===\n"
    diagnostics += "Current Time (UTC): %s\n" % Time.get_datetime_string_from_system(true)

    # Check system
    diagnostics += "\n--- SYSTEM INFORMATION ---\n"
    diagnostics += "Device ID: %s\n" % device_id
    diagnostics += "Device Model: %s\n" % device_model
    diagnostics += "OS Name: %s\n" % device_os_name
    diagnostics += "OS Version: %s\n" % device_os_version
    diagnostics += "System Name: %s\n" % system_name
    diagnostics += "Processor: %s\n" % processor_name
    diagnostics += "Rooted/Jailbroken: %s\n" % ("Yes" if is_rooted else "No")
    diagnostics += "Emulator: %s\n" % ("Yes" if is_emulator else "No")

    # Check storage
    diagnostics += "\n--- STORAGE ---\n"
    var storage_total_gb = float(storage_total) / (1024 * 1024 * 1024)
    var storage_available_gb = float(storage_available) / (1024 * 1024 * 1024)
    var storage_used_gb = (float(storage_total) - storage_available) / (1024 * 1024 * 1024)
    var storage_percent = 0.0 if storage_total == 0 else float(storage_total - storage_available) / float(storage_total) * 100.0

    diagnostics += "Total: %.2f GB\n" % storage_total_gb
    diagnostics += "Available: %.2f GB\n" % storage_available_gb
    diagnostics += "Used: %.2f GB (%.1f%%)\n" % [storage_used_gb, storage_percent]
    diagnostics += "Storage Warning: %s\n" % ("Yes" if is_storage_low() else "No")

    # Check network
    diagnostics += "\n--- NETWORK ---\n"
    diagnostics += "Status: %s\n" % get_network_type_string()
    diagnostics += "IP Address: %s\n" % ip_address
    diagnostics += "MAC Address: %s\n" % mac_address

    # Check screen
    diagnostics += "\n--- DISPLAY ---\n"
    diagnostics += "Size: %dx%d\n" % [screen_size.x, screen_size.y]
    diagnostics += "DPI: %.1f\n" % screen_dpi
    diagnostics += "Scale: %.2f\n" % screen_scale
    diagnostics += "Orientation: %s\n" % _get_orientation_string(screen_orientation)
    diagnostics += "Touchscreen: %s\n" % ("Yes" if is_touchscreen else "No")

    diagnostics += "\n=== DIAGNOSTICS COMPLETE ===\n"
    return diagnostics

##
# Sets the screen orientation (if supported by the platform)
##
func set_screen_orientation(orientation: int) -> bool:
    if not _is_mobile_platform():
        return false

    DisplayServer.screen_set_orientation(orientation)
    screen_orientation = DisplayServer.screen_get_orientation()
    emit_signal("screen_orientation_changed", screen_orientation)
    return true

##
# Keep screen on to prevent sleep (for kiosk mode)
##
func set_screen_always_on(enable: bool) -> void:
    DisplayServer.screen_set_keep_on(enable)

# ------------------------------------------------------------------
# Private Methods
# ------------------------------------------------------------------

##
# Generate a stable device ID based on hardware info
##
func _generate_device_id() -> String:
    # Combine unique identifiers to create a stable ID
    var base_string = OS.get_name() + "_" + OS.get_model_name() + "_" + str(OS.get_static_memory_usage())

    # Create a hash
    return str(hash(base_string)).replace("-", "")

##
# Check if device is rooted/jailbroken (basic detection)
##
func _check_if_rooted() -> bool:
    # Very basic check for rooted devices
    match OS.get_name():
        "Android":
            var su_paths = [
                "/system/app/Superuser.apk",
                "/system/xbin/su",
                "/system/bin/su",
                "/sbin/su",
                "/data/local/su"
            ]

            for path in su_paths:
                var file = FileAccess.open(path, FileAccess.READ)
                if file:
                    file.close()
                    return true
        "iOS":
            var jb_paths = [
                "/Applications/Cydia.app",
                "/Library/MobileSubstrate/MobileSubstrate.dylib",
                "/bin/bash",
                "/usr/sbin/sshd",
                "/etc/apt"
            ]

            for path in jb_paths:
                var file = FileAccess.open(path, FileAccess.READ)
                if file:
                    file.close()
                    return true

    return false

##
# Check if running on an emulator
##
func _check_if_emulator() -> bool:
    match OS.get_name():
        "Android":
            # Check common emulator indicators
            if device_model.to_lower().contains("sdk") or \
               device_model.to_lower().contains("emulator") or \
               device_model.to_lower().contains("android sdk"):
                return true

            # Check for typical emulator hardware IDs
            if device_unique_id.contains("emulator") or device_unique_id == "000000000000000":
                return true
        "iOS":
            # Check for simulator indicators (very basic)
            if device_model.to_lower().contains("simulator"):
                return true

    return false

##
# Update screen information
##
func _update_screen_info() -> void:
    # Get screen info if UI manager isn't available
    if not _ui_manager:
        screen_size = DisplayServer.screen_get_size()
        screen_dpi = DisplayServer.screen_get_dpi()
        is_touchscreen = DisplayServer.is_touchscreen_available()
        screen_scale = DisplayServer.screen_get_scale()
        screen_orientation = DisplayServer.screen_get_orientation()
    else:
        # Use UI manager data if available
        var ui_info = _ui_manager.get_info_as_dictionary()
        screen_size = ui_info.window_size
        is_touchscreen = ui_info.is_touchscreen

        # If screens array is populated, get first screen's info
        if ui_info.screens and ui_info.screens.size() > 0:
            screen_dpi = ui_info.screens[0].dpi
            screen_orientation = ui_info.screens[0].orientation

    # Fallback for screen scale if not set
    if screen_scale <= 0:
        screen_scale = 1.0

##
# Update network information
##
func _update_network_info() -> void:
    var old_network_status = network_status
    var old_ip_address = ip_address

    # Get list of IP addresses
    var addresses = IP.get_local_addresses()

    # Determine connection type and select an appropriate IP
    network_status = NetworkType.NONE
    ip_address = ""

    if _is_mobile_platform():
        # Mobile platform network detection
        # Check if we have any non-localhost IPs
        for addr in addresses:
            if addr != "127.0.0.1" and addr != "::1" and ":" not in addr:
                ip_address = addr
                network_status = NetworkType.WIFI
                break
    else:
        # Desktop platform network detection
        for addr in addresses:
            if addr != "127.0.0.1" and addr != "::1" and ":" not in addr:
                ip_address = addr
                network_status = NetworkType.ETHERNET
                break

    # If network status changed, emit signal
    if old_network_status != network_status or old_ip_address != ip_address:
        emit_signal("network_status_changed", {
            "network_status": network_status,
            "network_type": get_network_type_string(),
            "ip_address": ip_address
        })

        if old_network_status == NetworkType.NONE and network_status != NetworkType.NONE:
            if _logger:
                _logger.call("info", "DeviceManager: Network connected: %s" % get_network_type_string())
        elif old_network_status != NetworkType.NONE and network_status == NetworkType.NONE:
            if _logger:
                _logger.call("warn", "DeviceManager: Network disconnected")

##
# Update storage information
##
func _update_storage_info() -> void:
    # These would be replaced with real platform-specific implementations
    # Simulation for testing - 50GB total storage with 80% used
    storage_total = 50 * 1024 * 1024 * 1024 # 50 GB in bytes
    storage_available = 10 * 1024 * 1024 * 1024 # 10 GB in bytes

    # Check if storage is critically low
    if storage_available < STORAGE_WARNING_THRESHOLD:
        emit_signal("storage_warning", storage_available)
        if _logger:
            _logger.call("warn", "DeviceManager: Storage space critically low: %.2f MB available" % (storage_available / 1024.0 / 1024.0))

##
# Check if the device is running on a mobile platform
##
func _is_mobile_platform() -> bool:
    var os_name = OS.get_name()
    return os_name == "Android" or os_name == "iOS"

##
# Event handler for screen data updates from UIManager
##
func _on_screen_data_updated() -> void:
    _update_screen_info()

    if _logger:
        _logger.call("debug", "DeviceManager: Screen information updated")

##
# Helper function to get orientation as string
##
func _get_orientation_string(orientation: int) -> String:
    match orientation:
        DisplayServer.SCREEN_LANDSCAPE:
            return "Landscape"
        DisplayServer.SCREEN_PORTRAIT:
            return "Portrait"
        DisplayServer.SCREEN_REVERSE_LANDSCAPE:
            return "Reverse Landscape"
        DisplayServer.SCREEN_REVERSE_PORTRAIT:
            return "Reverse Portrait"
        DisplayServer.SCREEN_SENSOR_LANDSCAPE:
            return "Sensor Landscape"
        DisplayServer.SCREEN_SENSOR_PORTRAIT:
            return "Sensor Portrait"
        DisplayServer.SCREEN_SENSOR:
            return "Sensor"
    return "Unknown"
