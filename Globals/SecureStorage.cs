using Godot;
using ProjectTerminal.Globals.Interfaces;
using ProjectTerminal.Globals.Wrappers;


public partial class SecureStorage : Node, ISecureStorageWrapper
{
    private Logger _logger;
    private SecureStorageWrapper _storage;

    public override void _Ready()
    {
        _logger = GetNode<Logger>("/root/Logger");
        _logger.Info("SecureStorage: Initializing secure storage...");
        _storage = new SecureStorageWrapper(_logger);
    }

    // Delegate all interface methods to the wrapper implementation
    public bool StoreObject<T>(string key, T value) => _storage.StoreObject(key, value);
    public T RetrieveObject<T>(string key) => _storage.RetrieveObject<T>(key);
    public bool StoreValue(string key, object value) => _storage.StoreValue(key, value);
    public T RetrieveValue<T>(string key) => _storage.RetrieveValue<T>(key);
    public bool ClearValue(string key) => _storage.ClearValue(key);
    public bool HasKey(string key) => _storage.HasKey(key);
    public string[] GetAllKeys() => _storage.GetAllKeys();
}

