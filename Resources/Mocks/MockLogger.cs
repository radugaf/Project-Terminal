using Godot;
using System.Collections.Generic;

namespace ProjectTerminal.Resources.Mocks
{
    public partial class MockLogger : Node
    {
        public static void Debug(string message) { }
        public static void Info(string message) { }
        public static void Warn(string message) { }
        public static void Error(string message) { }
        public static void Error(string message, Dictionary<string, object> context) { }

        // Lower-case method aliases to match those in the actual Logger
        public static void debug(string message) { }
        public static void info(string message) { }
        public static void warn(string message) { }
        public static void error(string message) { }
    }
}
