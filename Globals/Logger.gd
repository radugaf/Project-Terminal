extends Node

enum LogLevel {
    DEBUG,
    INFO,
    WARN,
    ERROR,
    CRITICAL
}

const COLORS = {
    "DEBUG": "6699cc",
    "INFO": "88b04b",
    "WARN": "ffa500",
    "ERROR": "f44336",
    "CRITICAL": "9c27b0"
}

var _log_level: LogLevel = LogLevel.DEBUG
var _log_to_file: bool = false
var _log_file: FileAccess
var _log_path: String = "user://logs/"
var _max_log_size: int = 1024 * 1024 # 1 MB
var _max_log_files: int = 5

func _ready() -> void:
    if _log_to_file:
        _setup_file_logging()

func set_log_level(level: LogLevel) -> void:
    _log_level = level

func set_log_to_file(enable: bool) -> void:
    _log_to_file = enable
    if enable:
        _setup_file_logging()
    elif _log_file:
        _log_file.close()

func _setup_file_logging() -> void:
    var dir = DirAccess.open("user://")
    if not dir.dir_exists(_log_path):
        dir.make_dir(_log_path)
    
    var log_file_name = "godot_%s.log" % Time.get_date_string_from_system()
    _log_file = FileAccess.open(_log_path + log_file_name, FileAccess.WRITE)
    
    if not _log_file:
        push_error("Failed to open log file")
    else:
        _rotate_logs()

func _rotate_logs() -> void:
    var dir = DirAccess.open(_log_path)
    var files = dir.get_files()
    files.sort()
    
    while files.size() > _max_log_files:
        dir.remove(files[0])
        files.pop_front()
    
    if _log_file.get_length() > _max_log_size:
        _log_file.close()
        var new_log_file_name = "godot_%s_%s.log" % [Time.get_date_string_from_system(), Time.get_time_string_from_system().replace(":", "-")]
        _log_file = FileAccess.open(_log_path + new_log_file_name, FileAccess.WRITE)

func _log(message: String, level: LogLevel, context: Dictionary = {}) -> void:
    if level < _log_level:
        return

    var timestamp = Time.get_datetime_string_from_system()
    var level_string = LogLevel.keys()[level]
    var formatted_message = "[%s] [%s] %s" % [timestamp, level_string, message]
    
    if not context.is_empty():
        formatted_message += " | Context: %s" % str(context)
    
    if OS.is_debug_build():
        var color = Logger.COLORS[level_string]
        print_rich("[color=#%s]%s[/color]" % [color, formatted_message])
    else:
        print(formatted_message)
    
    if _log_to_file and _log_file:
        _log_file.store_line(formatted_message)
        _rotate_logs()

    if level == LogLevel.CRITICAL:
        push_error(formatted_message)
        # Optionally, you can add custom crash reporting here

func debug(message: String, context: Dictionary = {}) -> void:
    _log(message, LogLevel.DEBUG, context)

func info(message: String, context: Dictionary = {}) -> void:
    _log(message, LogLevel.INFO, context)

func warn(message: String, context: Dictionary = {}) -> void:
    _log(message, LogLevel.WARN, context)

func error(message: String, context: Dictionary = {}) -> void:
    _log(message, LogLevel.ERROR, context)

func critical(message: String, context: Dictionary = {}) -> void:
    _log(message, LogLevel.CRITICAL, context)