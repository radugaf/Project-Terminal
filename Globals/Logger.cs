using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class Logger : Node
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Critical = 4
    }

    private readonly Dictionary<LogLevel, Color> _colors = new()
    {
        { LogLevel.Debug, new Color("6699cc") },
        { LogLevel.Info, new Color("88b04b") },
        { LogLevel.Warn, new Color("ffa500") },
        { LogLevel.Error, new Color("f44336") },
        { LogLevel.Critical, new Color("9c27b0") }
    };

    private LogLevel _logLevel = LogLevel.Debug;
    private bool _logToFile = false;
    private Godot.FileAccess _logFile;
    private string _logPath = "user://logs/";
    private int _maxLogSize = 1024 * 1024; // 1 MB
    private int _maxLogFiles = 5;
    private bool _logToConsole = true;
    private string _applicationName = "POS Terminal";
    private bool _includeStackTrace = true;
    private bool _truncateLongMessages = false;
    private int _maxMessageLength = 10000;
    private readonly Dictionary<string, string> _additionalContextData = [];

    [Signal]
    public delegate void LogEntryAddedEventHandler(LogLevel level, string message);

    [Signal]
    public delegate void CriticalErrorOccurredEventHandler(string message, string stackTrace);

    public override void _Ready()
    {
        if (_logToFile)
        {
            SetupFileLogging();
        }
    }

    public override void _ExitTree()
    {
        _logFile?.Close();
    }

    public void SetLogLevel(LogLevel level)
    {
        _logLevel = level;
    }

    public void SetLogToFile(bool enable)
    {
        _logToFile = enable;
        if (enable)
        {
            SetupFileLogging();
        }
        else if (_logFile != null)
        {
            _logFile.Close();
            _logFile = null;
        }
    }

    public void SetLogToConsole(bool enable)
    {
        _logToConsole = enable;
    }

    public void SetIncludeStackTrace(bool enable)
    {
        _includeStackTrace = enable;
    }

    public void SetApplicationName(string name)
    {
        _applicationName = name;
    }

    public void SetMaxLogSize(int sizeInBytes)
    {
        _maxLogSize = sizeInBytes;
    }

    public void SetMaxLogFiles(int count)
    {
        _maxLogFiles = count;
    }

    public void AddGlobalContext(string key, string value)
    {
        _additionalContextData[key] = value;
    }

    public void RemoveGlobalContext(string key)
    {
        if (_additionalContextData.ContainsKey(key))
        {
            _additionalContextData.Remove(key);
        }
    }

    private void SetupFileLogging()
    {
        try
        {
            var dir = DirAccess.Open("user://");
            if (!dir.DirExists(_logPath))
            {
                dir.MakeDir(_logPath);
            }

            string logFileName = $"godot_{_applicationName.Replace(" ", "_")}_{DateTime.UtcNow:yyyy-MM-dd}.log";
            _logFile = Godot.FileAccess.Open(_logPath + logFileName, Godot.FileAccess.ModeFlags.Write);

            if (_logFile == null)
            {
                Error error = Godot.FileAccess.GetOpenError();
                Push(LogLevel.Error, $"Failed to open log file: {error}", new Dictionary<string, object> { { "path", _logPath + logFileName } });
            }
            else
            {
                RotateLogs();
            }
        }
        catch (Exception ex)
        {
            Push(LogLevel.Error, $"Failed to setup file logging: {ex.Message}", new Dictionary<string, object> { { "stack_trace", ex.StackTrace } });
        }
    }

    private void RotateLogs()
    {
        try
        {
            var dir = DirAccess.Open(_logPath);
            if (dir == null)
                return;

            var files = dir.GetFiles().Where(f => f.EndsWith(".log")).ToList();
            files.Sort();

            // Remove oldest files if we have too many
            while (files.Count > _maxLogFiles)
            {
                dir.Remove(files[0]);
                files.RemoveAt(0);
            }

            // Check current log file size
            if (_logFile != null && _logFile.GetLength() > (ulong)_maxLogSize)
            {
                string oldFilePath = _logFile.GetPath();
                _logFile.Close();

                // Create a new file with timestamp
                string newLogFileName = $"godot_{_applicationName.Replace(" ", "_")}_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.log";
                _logFile = Godot.FileAccess.Open(_logPath + newLogFileName, Godot.FileAccess.ModeFlags.Write);

                if (_logFile == null)
                {
                    Error error = Godot.FileAccess.GetOpenError();
                    Push(LogLevel.Error, $"Failed to create new log file during rotation: {error}", new Dictionary<string, object> { { "path", _logPath + newLogFileName } });
                }
            }
        }
        catch (Exception ex)
        {
            Push(LogLevel.Error, $"Log rotation failed: {ex.Message}", new Dictionary<string, object> { { "stack_trace", ex.StackTrace } });
        }
    }

    private void Push(LogLevel level, string message, Dictionary<string, object> context = null)
    {
        if (level < _logLevel)
            return;

        try
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffZ");
            string levelString = level.ToString().ToUpper();

            // Add caller information if enabled
            string callerInfo = string.Empty;
            if (_includeStackTrace && level >= LogLevel.Error)
            {
                System.Diagnostics.StackTrace stackTrace = new(2, true);
                System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                if (frame != null)
                {
                    string fileName = Path.GetFileName(frame.GetFileName() ?? "unknown");
                    int lineNumber = frame.GetFileLineNumber();
                    string methodName = frame.GetMethod()?.Name ?? "unknown";
                    callerInfo = $"[{fileName}:{lineNumber} in {methodName}]";
                }
            }

            // Format the main message
            string formattedMessage = $"[{timestamp}] [{levelString}]{(!string.IsNullOrEmpty(callerInfo) ? callerInfo : "")} {message}";

            // Add context if provided
            var allContext = new Dictionary<string, object>();

            // Add global context
            foreach (KeyValuePair<string, string> item in _additionalContextData)
            {
                allContext[item.Key] = item.Value;
            }

            // Add message-specific context, which can override global context
            if (context != null)
            {
                foreach (KeyValuePair<string, object> item in context)
                {
                    allContext[item.Key] = item.Value;
                }
            }

            if (allContext.Count > 0)
            {
                formattedMessage += $" | Context: {string.Join(", ", allContext.Select(kv => $"{kv.Key}={kv.Value}"))}";
            }

            // Truncate if needed
            if (_truncateLongMessages && formattedMessage.Length > _maxMessageLength)
            {
                formattedMessage = string.Concat(formattedMessage.AsSpan(0, _maxMessageLength), "... [message truncated]");
            }

            // Log to console
            if (_logToConsole)
            {
                if (OS.IsDebugBuild())
                {
                    Color color = _colors[level];
                    string colorHex = color.ToHtml(false);
                    GD.PrintRich($"[color=#{colorHex}]{formattedMessage}[/color]");
                }
                else
                {
                    GD.Print(formattedMessage);
                }
            }

            // Log to file
            if (_logToFile && _logFile != null)
            {
                _logFile.StoreLine(formattedMessage);
                RotateLogs();
            }

            // Emit signals
            EmitSignal(SignalName.LogEntryAdded, (int)level, formattedMessage);

            // Special handling for critical errors
            if (level == LogLevel.Critical)
            {
                string stackTrace = string.Empty;
                if (context != null && context.ContainsKey("stack_trace"))
                {
                    stackTrace = context["stack_trace"].ToString();
                }
                else if (_includeStackTrace)
                {
                    stackTrace = new System.Diagnostics.StackTrace(true).ToString();
                }

                EmitSignal(SignalName.CriticalErrorOccurred, message, stackTrace);
                GD.PushError(formattedMessage);
            }
        }
        catch (Exception ex)
        {
            // Fallback if our logging system fails
            GD.PrintErr($"Logger failed: {ex.Message}\nOriginal message: {message}");
        }
    }

    // Public logging methods
    public void Debug(string message, Dictionary<string, object> context = null)
    {
        Push(LogLevel.Debug, message, context);
    }

    public void Info(string message, Dictionary<string, object> context = null)
    {
        Push(LogLevel.Info, message, context);
    }

    public void Warn(string message, Dictionary<string, object> context = null)
    {
        Push(LogLevel.Warn, message, context);
    }

    public void Error(string message, Dictionary<string, object> context = null)
    {
        Push(LogLevel.Error, message, context);
    }

    public void Critical(string message, Dictionary<string, object> context = null)
    {
        Push(LogLevel.Critical, message, context);
    }

    // Convenience method for logging exceptions
    public void LogException(Exception ex, string message = null, LogLevel level = LogLevel.Error)
    {
        string logMessage = string.IsNullOrEmpty(message)
            ? $"Exception: {ex.Message}"
            : $"{message}: {ex.Message}";

        var context = new Dictionary<string, object>
        {
            { "exception_type", ex.GetType().Name },
            { "stack_trace", ex.StackTrace }
        };

        if (ex.InnerException != null)
        {
            context["inner_exception"] = ex.InnerException.Message;
        }

        Push(level, logMessage, context);
    }

    // Add these method aliases to your Logger class
    public void debug(string message, Dictionary<string, object> context = null) => Debug(message, context);
    public void info(string message, Dictionary<string, object> context = null) => Info(message, context);
    public void warn(string message, Dictionary<string, object> context = null) => Warn(message, context);
    public void error(string message, Dictionary<string, object> context = null) => Error(message, context);
    public void critical(string message, Dictionary<string, object> context = null) => Critical(message, context);
}
