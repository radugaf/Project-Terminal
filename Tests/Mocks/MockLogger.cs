// Tests/Mocks/MockLogger.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTerminal.Tests.Mocks
{
    public partial class MockLogger : Logger
    {
        public class LogEntry
        {
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public Dictionary<string, object> Context { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;

            public override string ToString()
            {
                string contextStr = Context != null && Context.Count > 0
                    ? $" | Context: {string.Join(", ", Context.Select(kv => $"{kv.Key}={kv.Value}"))}"
                    : "";

                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Message}{contextStr}";
            }
        }

        public List<LogEntry> Entries { get; } = new();

        // Flags to check if specific level methods were called
        public bool DebugCalled => Entries.Any(e => e.Level == LogLevel.Debug);
        public bool InfoCalled => Entries.Any(e => e.Level == LogLevel.Info);
        public bool WarnCalled => Entries.Any(e => e.Level == LogLevel.Warn);
        public bool ErrorCalled => Entries.Any(e => e.Level == LogLevel.Error);
        public bool CriticalCalled => Entries.Any(e => e.Level == LogLevel.Critical);

        // Get all entries of a specific level
        public List<LogEntry> GetEntriesOfLevel(LogLevel level) => Entries.Where(e => e.Level == level).ToList();

        // Check if a specific message was logged
        public bool ContainsMessage(string message, LogLevel? level = null)
        {
            return level.HasValue
                ? Entries.Any(e => e.Message.Contains(message) && e.Level == level.Value)
                : Entries.Any(e => e.Message.Contains(message));
        }

        // Find specific log entries
        public List<LogEntry> FindEntries(Func<LogEntry, bool> predicate) => Entries.Where(predicate).ToList();

        // Get the last log entry
        public LogEntry LastEntry => Entries.Count > 0 ? Entries[Entries.Count - 1] : null;

        // Clear all recorded entries
        public void Clear() => Entries.Clear();

        // Implement logger methods to record calls
        public new void Debug(string message, Dictionary<string, object> context = null) => Entries.Add(new LogEntry { Level = LogLevel.Debug, Message = message, Context = context });
        public new void Info(string message, Dictionary<string, object> context = null) => Entries.Add(new LogEntry { Level = LogLevel.Info, Message = message, Context = context });
        public new void Warn(string message, Dictionary<string, object> context = null) => Entries.Add(new LogEntry { Level = LogLevel.Warn, Message = message, Context = context });
        public new void Error(string message, Dictionary<string, object> context = null) => Entries.Add(new LogEntry { Level = LogLevel.Error, Message = message, Context = context });
        public new void Critical(string message, Dictionary<string, object> context = null) => Entries.Add(new LogEntry { Level = LogLevel.Critical, Message = message, Context = context });

        // Add the gdscript-style method aliases too
        public new void debug(string message, Dictionary<string, object> context = null) => Debug(message, context);
        public new void info(string message, Dictionary<string, object> context = null) => Info(message, context);
        public new void warn(string message, Dictionary<string, object> context = null) => Warn(message, context);
        public new void error(string message, Dictionary<string, object> context = null) => Error(message, context);
        public new void critical(string message, Dictionary<string, object> context = null) => Critical(message, context);

        // Helper for exception logging
        public new void LogException(Exception ex, string message = null, LogLevel level = LogLevel.Error)
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

            Entries.Add(new LogEntry { Level = level, Message = logMessage, Context = context });
        }

        // Helper to dump all logs to console (useful for debugging tests)
        public void DumpLogs()
        {
            foreach (LogEntry entry in Entries)
            {
                Console.WriteLine(entry.ToString());
            }
        }
    }
}
