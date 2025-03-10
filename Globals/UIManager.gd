# UIManager.gd
extends Node

# -----------------------------------------------------------------------
# PROPERTIES
# -----------------------------------------------------------------------

# Emitted whenever display information is updated, whether automatically
# or through a manual call to retrieve_display_info().
signal screen_data_updated

# Stores the OS name, e.g. "Windows", "Android", "iOS", etc.
var system_name: String
# Number of screens (monitors) detected.
var screen_count: int
# Array of dictionaries containing per-screen data (size, DPI, orientation).
var screens: Array
# Current Godot window size (in pixels).
var window_size: Vector2
# Whether the OS reports a touchscreen interface (mobile, tablet, etc.).
var is_touchscreen: bool

# -----------------------------------------------------------------------
# LIFECYCLE
# -----------------------------------------------------------------------
func _ready() -> void:
	retrieve_display_info()
	get_viewport().connect("size_changed", _on_viewport_resized)

# -----------------------------------------------------------------------
# PUBLIC API
# -----------------------------------------------------------------------

##
# Gathers fresh display info and stores it in this node's properties.
# Emits screen_data_updated so other scripts can react.
##
func retrieve_display_info() -> void:
	system_name = OS.get_name()

	# Number of monitors.
	screen_count = DisplayServer.get_screen_count()

	# Gather per-screen info in a list of dictionaries.
	screens = []
	for screen_index in range(screen_count):
		var size = DisplayServer.screen_get_size(screen_index)
		var dpi = DisplayServer.screen_get_dpi(screen_index)
		var orientation = DisplayServer.screen_get_orientation(screen_index)
		screens.append({
			"index": screen_index,
			"size": size,
			"dpi": dpi,
			"orientation": orientation
		})

	# Window size (the current Godot window size in pixels).
	window_size = DisplayServer.window_get_size()

	# Whether it's a touchscreen environment (mobile/tablet).
	is_touchscreen = DisplayServer.is_touchscreen_available()

	# Notify listeners that data was updated.
	emit_signal("screen_data_updated")

##
# Returns a Dictionary with all the stored display/system properties.
# Helpful if you want everything in a single object.
##
func get_info_as_dictionary() -> Dictionary:
	return {
		"system_name": system_name,
		"screen_count": screen_count,
		"screens": screens,
		"window_size": window_size,
		"is_touchscreen": is_touchscreen
	}

# -----------------------------------------------------------------------
# PRIVATE HANDLERS
# -----------------------------------------------------------------------

##
# Called automatically when the window (the root viewport) is resized.
# We refresh the display info to keep it up to date.
##
func _on_viewport_resized() -> void:
	retrieve_display_info()
