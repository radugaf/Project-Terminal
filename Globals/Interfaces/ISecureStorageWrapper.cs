namespace ProjectTerminal.Globals.Interfaces
{
    public interface ISecureStorageWrapper
    {
        bool StoreObject<T>(string key, T value);
        T RetrieveObject<T>(string key);
        bool StoreValue(string key, object value);
        T RetrieveValue<T>(string key);
        bool ClearValue(string key);
        bool HasKey(string key);
        string[] GetAllKeys();
    }
}
