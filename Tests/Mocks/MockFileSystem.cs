using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTerminal.Globals.Interfaces;

namespace ProjectTerminal.Tests.Mocks
{
    public class MockFileSystem(Logger logger) : IFileSystem
    {
        private readonly Dictionary<string, string> _files = [];
        private readonly HashSet<string> _directories = [];
        private readonly Logger _logger = logger;

        public bool FileExists(string path)
        {
            bool exists = _files.ContainsKey(NormalizePath(path));
            _logger.Debug($"MockFileSystem: FileExists({path}) => {exists}");
            return exists;
        }

        public string ReadAllText(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!_files.TryGetValue(normalizedPath, out string value))
            {
                _logger.Debug($"MockFileSystem: ReadAllText({path}) => file not found");
                return null;
            }

            string content = value;
            _logger.Debug($"MockFileSystem: ReadAllText({path}) => length {content.Length}");
            return content;
        }

        public bool WriteAllText(string path, string content)
        {
            try
            {
                string normalizedPath = NormalizePath(path);
                string dir = GetDirectoryPath(normalizedPath);

                // Ensure directory exists
                if (!_directories.Contains(dir))
                {
                    _logger.Debug($"MockFileSystem: WriteAllText - directory {dir} doesn't exist for {path}");
                    return false;
                }

                _files[normalizedPath] = content;
                _logger.Debug($"MockFileSystem: WriteAllText({path}) => success, wrote {content.Length} chars");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"MockFileSystem: WriteAllText exception: {ex.Message}");
                return false;
            }
        }

        public bool DeleteFile(string path)
        {
            string normalizedPath = NormalizePath(path);
            bool removed = _files.Remove(normalizedPath);
            _logger.Debug($"MockFileSystem: DeleteFile({path}) => {removed}");
            return true; // Always succeed, even if file wasn't there
        }

        public bool CreateDirectory(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (normalizedPath.StartsWith("user://"))
            {
                _directories.Add(normalizedPath);
                _logger.Debug($"MockFileSystem: CreateDirectory({path}) => success");
                return true;
            }

            // Also add with user:// prefix if it's not already there
            _directories.Add("user://" + normalizedPath);
            _logger.Debug($"MockFileSystem: CreateDirectory({path}) => success with user:// prefix");
            return true;
        }

        public bool DirectoryExists(string path)
        {
            string normalizedPath = NormalizePath(path);
            bool exists = _directories.Contains(normalizedPath);

            // If doesn't exist with user:// prefix, check without it
            if (!exists && !normalizedPath.StartsWith("user://") && _directories.Contains("user://" + normalizedPath))
            {
                exists = true;
            }

            _logger.Debug($"MockFileSystem: DirectoryExists({path}) => {exists}");
            return exists;
        }

        public string[] GetFilesInDirectory(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (!normalizedPath.EndsWith("/"))
            {
                normalizedPath += "/";
            }

            // Find all files that start with this directory path
            string[] result = [.. _files.Keys
                .Where(filePath => filePath.StartsWith(normalizedPath))
                .Select(filePath => GetFileName(filePath))];

            _logger.Debug($"MockFileSystem: GetFilesInDirectory({path}) => {result.Length} files");
            return result;
        }

        // Helper to normalize path separators
        private static string NormalizePath(string path) => path.Replace("\\", "/");

        // Get directory part of a path
        private static string GetDirectoryPath(string path)
        {
            int lastSlash = path.LastIndexOf('/');
            return lastSlash <= 0 ? "user://" : path[..lastSlash];
        }

        // Get filename part of a path
        private static string GetFileName(string path)
        {
            int lastSlash = path.LastIndexOf('/');
            return lastSlash < 0 || lastSlash == path.Length - 1 ? path : path[(lastSlash + 1)..];
        }

        // Helper methods for testing to directly set file content
        public void SetFileContent(string path, string content) => _files[NormalizePath(path)] = content;

        // Helper to directly get file content for verification
        public string GetFileContent(string path)
        {
            string normalizedPath = NormalizePath(path);
            return _files.TryGetValue(normalizedPath, out string value) ? value : null;
        }

        // Reset all mock state for clean test runs
        public void Reset()
        {
            _files.Clear();
            _directories.Clear();
            // _directories.Add("user://");
            // _directories.Add("user://secure_data");
        }
    }
}
