using ProjectTerminal.Globals.Interfaces;
using System;
using System.Linq;
using System.Text.Json;

namespace ProjectTerminal.Globals.Wrappers
{
    public class SecureStorageWrapper : ISecureStorageWrapper
    {
        public SecureStorageWrapper(Logger logger, IFileSystem fileSystem = null)
        {
            _logger = logger;
            _fileSystem = fileSystem ?? new GodotFileSystem();
            _storagePath = "user://" + STORAGE_DIR;
            InitializeStorage();
        }

        private const string STORAGE_DIR = "secure_data";
        private const string FILE_EXTENSION = ".dat";

        private readonly Logger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private void InitializeStorage()
        {
            if (!_fileSystem.DirectoryExists(STORAGE_DIR))
            {
                bool success = _fileSystem.CreateDirectory(STORAGE_DIR);
                if (!success)
                {
                    _logger.Error("SecureStorageWrapper: Failed to create storage directory");
                }
            }
        }

        private string GetPathForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }

            string safeKey = key.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            return _storagePath + "/" + safeKey + FILE_EXTENSION;
        }

        public bool StoreObject<T>(string key, T value)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    _logger.Error("SecureStorageWrapper: Key cannot be null or empty");
                    return false;
                }

                if (value == null)
                {
                    _logger.Error("SecureStorageWrapper: Value cannot be null");
                    return false;
                }

                string json = JsonSerializer.Serialize(value, _jsonOptions);
                string filePath = GetPathForKey(key);

                bool success = _fileSystem.WriteAllText(filePath, json);
                if (!success)
                {
                    _logger.Error("SecureStorageWrapper: Failed to write file");
                    return false;
                }

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

                if (!_fileSystem.FileExists(filePath))
                {
                    _logger.Debug($"SecureStorageWrapper: No value for key '{key}'");
                    return default;
                }

                string json = _fileSystem.ReadAllText(filePath);
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

        // Implement other interface methods similarly...

        public bool StoreValue(string key, object value)
        {
            try
            {
                if (value == null)
                    return ClearValue(key);

                string filePath = GetPathForKey(key);
                string typeName = value.GetType().FullName;
                string stringValue = value is string strValue
                    ? strValue
                    : value is int or float or double or bool or DateTime
                    ? value.ToString()
                    : throw new ArgumentException($"Unsupported type: {value.GetType().Name}");

                string content = $"{typeName}\n{stringValue}";
                bool success = _fileSystem.WriteAllText(filePath, content);

                if (!success)
                {
                    _logger.Error("SecureStorageWrapper: Failed to write file");
                    return false;
                }

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

                if (!_fileSystem.FileExists(filePath))
                {
                    return default;
                }

                string content = _fileSystem.ReadAllText(filePath);
                string[] lines = content.Split('\n');

                if (lines.Length < 2)
                {
                    return default;
                }

                string typeName = lines[0];
                string stringValue = lines[1];

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
                    : targetType == typeof(DateTime) && DateTime.TryParse(stringValue, out DateTime dateValue)
                    ? (T)(object)dateValue
                    : default;
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
                return !_fileSystem.FileExists(filePath) || _fileSystem.DeleteFile(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Clear error: {ex.Message}");
                return false;
            }
        }

        public bool HasKey(string key)
        {
            try
            {
                string filePath = GetPathForKey(key);
                return _fileSystem.FileExists(filePath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string[] GetAllKeys()
        {
            try
            {
                if (!_fileSystem.DirectoryExists(STORAGE_DIR))
                {
                    return Array.Empty<string>();
                }

                string[] files = _fileSystem.GetFilesInDirectory(_storagePath);
                return files
                    .Select(f => System.IO.Path.GetFileNameWithoutExtension(f))
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.Error($"SecureStorageWrapper: Failed to get all keys: {ex.Message}");
                return Array.Empty<string>();
            }
        }
    }
}
