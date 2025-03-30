// ProjectTerminal/Globals/Wrappers/GodotFileSystem.cs
using Godot;
using ProjectTerminal.Globals.Interfaces;
using System;
using System.Linq;

namespace ProjectTerminal.Globals.Wrappers
{
    public class GodotFileSystem : IFileSystem
    {
        public bool FileExists(string path)
        {
            return FileAccess.FileExists(path);
        }

        public string ReadAllText(string path)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return null;
            }
            return file.GetAsText();
        }

        public bool WriteAllText(string path, string content)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                return false;
            }
            file.StoreString(content);
            return true;
        }

        public bool DeleteFile(string path)
        {
            if (!FileAccess.FileExists(path))
            {
                return true;
            }

            // Godot doesn't have a direct method to delete files,
            // we need to use DirAccess
            string directory = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string filename = System.IO.Path.GetFileName(path);

            var dir = DirAccess.Open(directory);
            if (dir == null)
            {
                return false;
            }

            return dir.Remove(filename) == Error.Ok;
        }

        public bool CreateDirectory(string path)
        {
            var dir = DirAccess.Open("user://");
            if (dir == null)
            {
                return false;
            }

            return dir.MakeDir(path) == Error.Ok;
        }

        public bool DirectoryExists(string path)
        {
            var dir = DirAccess.Open("user://");
            if (dir == null)
            {
                return false;
            }

            return dir.DirExists(path);
        }

        public string[] GetFilesInDirectory(string path)
        {
            var dir = DirAccess.Open(path);
            if (dir == null)
            {
                return Array.Empty<string>();
            }

            return dir.GetFiles();
        }
    }
}
