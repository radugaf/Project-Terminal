using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class SecureStorage : Node
{
    private const string STORAGE_DIR = "secure_data";
    private const string FILE_EXTENSION = ".dat";

    private Node _logger;
    private string _storagePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override void _Ready()
    {
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "SecureStorage: Initializing secure storage...");
        InitializeStorage();
    }

    private void InitializeStorage()
    {
        _storagePath = "user://" + STORAGE_DIR;

        var dir = DirAccess.Open("user://");
        if (dir != null)
        {
            if (!dir.DirExists(STORAGE_DIR))
            {
                Error err = dir.MakeDir(STORAGE_DIR);
                if (err != Error.Ok)
                {
                    _logger.Call("error", $"SecureStorage: Failed to create storage directory: {err}");
                }
            }
        }
        else
        {
            _logger.Call("error", $"SecureStorage: Cannot access user directory: {DirAccess.GetOpenError()}");
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
                _logger.Call("error", $"Failed to write file: {FileAccess.GetOpenError()}");
                return false;
            }

            file.StoreString(json);
            _logger.Call("debug", $"SecureStorage: Object stored under key '{key}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Failed to store object: {ex.Message}");
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
                _logger.Call("debug", $"SecureStorage: No value for key '{key}'");
                return default;
            }

            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                _logger.Call("error", $"Failed to read file: {FileAccess.GetOpenError()}");
                return default;
            }

            string json = file.GetAsText();

            if (string.IsNullOrEmpty(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException jsonEx)
        {
            _logger.Call("error", $"SecureStorage: JSON parse error: {jsonEx.Message}");
            return default;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Retrieval error: {ex.Message}");
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
                _logger.Call("error", $"Failed to write file: {FileAccess.GetOpenError()}");
                return false;
            }

            file.StoreLine(typeName);
            file.StoreLine(stringValue);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Failed to store value: {ex.Message}");
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
                _logger.Call("error", $"Failed to read file: {FileAccess.GetOpenError()}");
                return default;
            }

            string typeName = file.GetLine();
            string stringValue = file.GetLine();

            if (string.IsNullOrEmpty(stringValue))
            {
                return default;
            }

            Type targetType = typeof(T);

            if (targetType == typeof(string))
                return (T)(object)stringValue;
            if (targetType == typeof(int) && int.TryParse(stringValue, out int intValue))
                return (T)(object)intValue;
            if (targetType == typeof(bool) && bool.TryParse(stringValue, out bool boolValue))
                return (T)(object)boolValue;
            if (targetType == typeof(float) && float.TryParse(stringValue, out float floatValue))
                return (T)(object)floatValue;
            if (targetType == typeof(double) && double.TryParse(stringValue, out double doubleValue))
                return (T)(object)doubleValue;
            if (targetType == typeof(DateTime) && DateTime.TryParse(stringValue, out DateTime dateValue))
                return (T)(object)dateValue;

            return default;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Retrieval error: {ex.Message}");
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
            _logger.Call("error", $"SecureStorage: Clear error: {ex.Message}");
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
                return Array.Empty<string>();
            }

            string[] files = dir.GetFiles();
            return files.Select(f => f.TrimSuffix(FILE_EXTENSION)).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
