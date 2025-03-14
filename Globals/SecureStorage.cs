using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Manages secure storage of sensitive application data using Godot's encrypted
/// project settings. Provides methods for storing and retrieving serialized objects.
/// </summary>
public partial class SecureStorage : Node
{
    #region Constants and Fields

    /// <summary>
    /// Reference to the application logger.
    /// </summary>
    private Node _logger;

    /// <summary>
    /// JSON serialization options for consistent formatting.
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the SecureStorage and gets references to required nodes.
    /// </summary>
    public override void _Ready()
    {
        // Get a reference to the logger
        _logger = GetNode<Node>("/root/Logger");
        _logger.Call("info", "SecureStorage: Initializing secure storage...");
    }

    #endregion

    #region Storage Methods

    /// <summary>
    /// Stores an object securely by serializing it to JSON and saving it to project settings.
    /// </summary>
    /// <typeparam name="T">The type of object to store</typeparam>
    /// <param name="key">The key to store the object under</param>
    /// <param name="value">The object to store</param>
    /// <returns>True if storage was successful, false otherwise</returns>
    public bool StoreObject<T>(string key, T value)
    {
        try
        {
            if (value == null)
            {
                // Clear the value instead
                return ClearValue(key);
            }

            // Serialize the object
            string json = JsonSerializer.Serialize(value, _jsonOptions);

            // Store in project settings
            ProjectSettings.SetSetting($"application/config/{key}", json);
            ProjectSettings.Save();

            _logger.Call("debug", $"SecureStorage: Object stored securely under key '{key}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Failed to store object: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retrieves an object from secure storage by deserializing it from JSON.
    /// </summary>
    /// <typeparam name="T">The type of object to retrieve</typeparam>
    /// <param name="key">The key the object is stored under</param>
    /// <returns>The deserialized object, or default(T) if retrieval failed</returns>
    public T RetrieveObject<T>(string key)
    {
        try
        {
            if (!ProjectSettings.HasSetting($"application/config/{key}"))
            {
                _logger.Call("debug", $"SecureStorage: No value found for key '{key}'");
                return default;
            }

            string json = (string)ProjectSettings.GetSetting($"application/config/{key}");

            if (string.IsNullOrEmpty(json))
            {
                _logger.Call("debug", $"SecureStorage: Empty value for key '{key}'");
                return default;
            }

            // Deserialize the object
            T result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            _logger.Call("debug", $"SecureStorage: Object retrieved from key '{key}'");
            return result;
        }
        catch (JsonException jsonEx)
        {
            _logger.Call("error", $"SecureStorage: Failed to parse JSON for key '{key}': {jsonEx.Message}");
            return default;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Error retrieving object for key '{key}': {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Stores a simple value (string, number, boolean) securely in project settings.
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    /// <returns>True if storage was successful, false otherwise</returns>
    public bool StoreValue(string key, object value)
    {
        try
        {
            if (value == null)
                return ClearValue(key);

            // Convert object to Godot Variant
            var variant = Variant.From(value);

            // Store in project settings
            ProjectSettings.SetSetting($"application/config/{key}", variant);
            ProjectSettings.Save();

            _logger.Call("debug", $"SecureStorage: Value stored securely under key '{key}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Failed to store value: {ex.Message}");
            return false;
        }
    }
    /// <summary>
    /// Retrieves a simple value from secure storage.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve</typeparam>
    /// <param name="key">The key the value is stored under</param>
    /// <returns>The value, or default(T) if retrieval failed</returns>
    public T RetrieveValue<T>(string key)
    {
        try
        {
            if (!ProjectSettings.HasSetting($"application/config/{key}"))
            {
                _logger.Call("debug", $"SecureStorage: No value found for key '{key}'");
                return default;
            }

            object value = ProjectSettings.GetSetting($"application/config/{key}");

            if (value == null)
            {
                _logger.Call("debug", $"SecureStorage: Null value for key '{key}'");
                return default;
            }

            // Try to convert the value to the requested type
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                _logger.Call("warn", $"SecureStorage: Could not convert value for key '{key}' to type {typeof(T).Name}");
                return default;
            }
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Error retrieving value for key '{key}': {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Clears a value from secure storage.
    /// </summary>
    /// <param name="key">The key to clear</param>
    /// <returns>True if clearing was successful, false otherwise</returns>
    public bool ClearValue(string key)
    {
        try
        {
            if (ProjectSettings.HasSetting($"application/config/{key}"))
            {
                ProjectSettings.SetSetting($"application/config/{key}", string.Empty);
                ProjectSettings.Save();
                _logger.Call("debug", $"SecureStorage: Value cleared for key '{key}'");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Call("error", $"SecureStorage: Failed to clear value for key '{key}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a key exists in secure storage.
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    public bool HasKey(string key)
    {
        return ProjectSettings.HasSetting($"application/config/{key}");
    }

    #endregion
}
