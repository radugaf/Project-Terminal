// ProjectTerminal/Globals/Interfaces/IFileSystem.cs
namespace ProjectTerminal.Globals.Interfaces
{
    public interface IFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        bool WriteAllText(string path, string content);
        bool DeleteFile(string path);
        bool CreateDirectory(string path);
        bool DirectoryExists(string path);
        string[] GetFilesInDirectory(string path);
    }
}
