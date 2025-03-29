// SecureStorageWrapper.cs
using Godot;
using ProjectTerminal.Globals.Interfaces;
using System;
using System.Linq;
using System.Text.Json;

namespace ProjectTerminal.Globals.Wrappers
{
    public class SecureStorageWrapper : ISecureStorageWrapper
    {
        public SecureStorageWrapper(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storagePath = "user://" + STORAGE_DIR;
            InitializeStorage();
        }

        private const string STORAGE_DIR = "secure_data";
        private const string FILE_EXTENSION = ".dat";

        private readonly Logger _logger;
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private void InitializeStorage()
        {
            var dir = DirAccess.Open("user://");
            if (dir != null)
            {
                if (!dir.DirExists(STORAGE_DIR))
                {
                    Error err = dir.MakeDir(STORAGE_DIR);
                    if (err != Error.Ok)
                    {
                        _logger.Error($"SecureStorageWrapper: Failed to create storage directory: {err}");
                    }
                }
            }
            else
            {
                _logger.Error($"SecureStorageWrapper: Cannot access user directory: {DirAccess.GetOpenError()}");
            }
        }

        private string GetPathForKey(string key)
        {
            string safeKey = key.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            return _storagePath.PathJoin(safeKey + FILE_EXTENSION);
        }

        public bool StoreObject<T>(string key, T value)
        {
            try
            {
                if (value == null)
                {
                    return ClearValue(key);
                }

                string json = JsonSerializer.Serialize(value, _jsonOptions);
                string filePath = GetPathForKey(key);

                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    _logger.Error($"Failed to write file: {FileAccess.GetOpenError()}");
                    return false;
                }

                file.StoreString(json);
                _logger.Debug($"SecureStorageWrapper: Object stored under key '{key}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Failed to store object: {ex.Message}");
                return false;
            }
        }

        public T RetrieveObject<T>(string key)
        {
            try
            {
                string filePath = GetPathForKey(key);

                if (!FileAccess.FileExists(filePath))
                {
                    _logger.Debug($"SecureStorageWrapper: No value for key '{key}'");
                    return default;
                }

                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    _logger.Error($"Failed to read file: {FileAccess.GetOpenError()}");
                    return default;
                }

                string json = file.GetAsText();

                return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.Error($"SecureStorageWrapper: JSON parse error: {jsonEx.Message}");
                return default;
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Retrieval error: {ex.Message}");
                return default;
            }
        }

        public bool StoreValue(string key, object value)
        {
            try
            {
                if (value == null)
                    return ClearValue(key);

                string filePath = GetPathForKey(key);
                string typeName = value.GetType().FullName;
                string stringValue;

                if (value is string strValue)
                    stringValue = strValue;
                else if (value is int or float or double or bool or DateTime)
                    stringValue = value.ToString();
                else
                    throw new ArgumentException($"Unsupported type: {value.GetType().Name}");

                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    _logger.Error($"Failed to write file: {FileAccess.GetOpenError()}");
                    return false;
                }

                file.StoreLine(typeName);
                file.StoreLine(stringValue);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Failed to store value: {ex.Message}");
                return false;
            }
        }

        public T RetrieveValue<T>(string key)
        {
            try
            {
                string filePath = GetPathForKey(key);

                if (!FileAccess.FileExists(filePath))
                {
                    return default;
                }

                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    _logger.Error($"Failed to read file: {FileAccess.GetOpenError()}");
                    return default;
                }

                string typeName = file.GetLine();
                string stringValue = file.GetLine();

                if (string.IsNullOrEmpty(stringValue))
                {
                    return default;
                }

                Type targetType = typeof(T);

                return targetType == typeof(string)
                    ? (T)(object)stringValue
                    : targetType == typeof(int) && int.TryParse(stringValue, out int intValue)
                    ? (T)(object)intValue
                    : targetType == typeof(bool) && bool.TryParse(stringValue, out bool boolValue)
                    ? (T)(object)boolValue
                    : targetType == typeof(float) && float.TryParse(stringValue, out float floatValue)
                    ? (T)(object)floatValue
                    : targetType == typeof(double) && double.TryParse(stringValue, out double doubleValue)
                    ? (T)(object)doubleValue
                    : targetType == typeof(DateTime) && DateTime.TryParse(stringValue, out DateTime dateValue) ? (T)(object)dateValue : default;
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Retrieval error: {ex.Message}");
                return default;
            }
        }

        public bool ClearValue(string key)
        {
            try
            {
                string filePath = GetPathForKey(key);

                if (FileAccess.FileExists(filePath))
                {
                    var dir = DirAccess.Open(_storagePath.GetBaseDir());
                    if (dir != null)
                    {
                        return dir.Remove(filePath) == Error.Ok;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Clear error: {ex.Message}");
                return false;
            }
        }

        public bool HasKey(string key)
        {
            return FileAccess.FileExists(GetPathForKey(key));
        }

        public string[] GetAllKeys()
        {
            try
            {
                var dir = DirAccess.Open(_storagePath);
                if (dir == null)
                {
                    return [];
                }

                string[] files = dir.GetFiles();
                return files.Select(f => f.TrimSuffix(FILE_EXTENSION)).ToArray();
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Failed to get all keys: {ex.Message}");
                return [];
            }
        }
    }
}
