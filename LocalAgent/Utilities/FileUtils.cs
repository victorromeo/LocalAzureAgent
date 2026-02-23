using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LocalAgent.Variables;

namespace LocalAgent.Utilities
{
    public class FileUtils
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public byte[] GetMd5HashBytes(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            return md5.ComputeHash(stream);
        }

        /// <summary>
        /// Helper function to calculate a MD5 hash for a file
        /// </summary>
        /// <param name="filePath">File path to hash</param>
        /// <returns>Lowercase string containing the file hash</returns>
        public virtual string GetMd5Hash(string filePath)
        {
            return BitConverter.ToString(GetMd5HashBytes(filePath))
                .Replace("-", "").ToLower();
        }

        /// <summary>
        /// Helper function to check if a file exists and has a specified extension
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public virtual bool CheckFileExtension(FileInfo fileInfo, string extension)
        {
            return fileInfo.Exists
                   && fileInfo.Extension.ToLower() == extension.ToLower();
        }

        /// <summary>
        /// Supports recursive searches for a file, by filename
        /// </summary>
        /// <param name="basePath">The base folder to search</param>
        /// <param name="fileName">The name of the file to seek</param>
        /// <param name="recursive">Default true</param>
        /// <returns>A list of fully qualified file paths which match the request</returns>
        public virtual IList<string> FindFiles(string basePath,string fileName, bool recursive = true)
        {
            var searchDepth = recursive 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;

            if (new DirectoryInfo(basePath).Exists)
                return Directory.GetFiles(basePath,fileName, searchDepth);
            return new List<string>();
        }

        public virtual IList<string> FindFile(string path)
        {
            return new FileInfo(path).Exists
                ? new List<string> {path}
                : new List<string>();
        }

        /// <summary>
        /// Copies all the files from one folder into another, in parallel
        /// </summary>
        /// <param name="sourcePath">Folder content to copy</param>
        /// <param name="destinationPath">Folder destination to receive content</param>
        public void CloneFolder(string sourcePath, string destinationPath)
        {
            GuardPreventDamageToSystemFolders(sourcePath);
            GuardPreventDamageToSystemFolders(destinationPath);

            var sourceDirectory = new DirectoryInfo(sourcePath);
            if (!sourceDirectory.Exists)
            {
                throw new ArgumentException($"Folder not found: '{sourcePath}'", nameof(sourcePath));
            }

            sourceDirectory.CopyTo(destinationPath);
        }

        /// <summary>
        /// Deletes the content in a specified folder
        /// First delete the whole folder, then recreates an empty folder
        /// </summary>
        /// <param name="path"></param>
        public void DeleteFolderContent(string path)
        {
            GuardPreventDamageToSystemFolders(path);

            var info = new DirectoryInfo(path);
            if (!info.Exists)
            {
                throw new ArgumentException($"Folder not found: '{path}'", nameof(path));
            }

            var paths = Directory.EnumerateDirectories(path);

            // ReadOnly flags must be cleared before the folder content is deleted
            ClearReadOnlyFlag(path);

            info.Delete(true);
            info.Create();
        }

        /// Recursive operation to clear Read Only flags from Files and Directories
        public void ClearReadOnlyFlag(string path) {

            GuardPreventDamageToSystemFolders(path);

            Logger.Info($"Clearing ReadOnly flags in {path}");

            new DirectoryInfo(path).GetDirectories("*", SearchOption.AllDirectories)
                .ToList().ForEach(
                    di => {
                        di.Attributes &= ~FileAttributes.ReadOnly;
                        di.GetFiles("*", SearchOption.TopDirectoryOnly)
                            .ToList()
                            .ForEach(fi => fi.IsReadOnly = false);
                    }
                );
        }

        // Creates a folder, and subfolders
        public void CreateFolder(string path)
        {
            GuardPreventDamageToSystemFolders(path);

            if (Directory.Exists(path))
            {
                Logger.Info($"Create Folder: {path}. Exists skipping");
            }
            else
            {
                Directory.CreateDirectory(path);
                Logger.Info($"Create Folder: {path}. Created");
            }
        }

        public IList<string> FindFilesByPattern(PipelineContext context, string path, IList<string> patterns)
        {
            var buildTargets = new List<string>();

            foreach (var s in patterns)
            {
                if (s.StartsWith("**/*."))
                {
                    var searchExtension = s.Replace("**/*.", "*.");
                    buildTargets.AddRange(FindFiles(context.Variables[VariableNames.BuildSourcesDirectory], searchExtension, true));
                }
                else if (s.StartsWith("*."))
                {
                    buildTargets.AddRange(FindFiles(context.Variables[VariableNames.BuildSourcesDirectory], s, false));
                }
                else
                {
                    var searchPath = Path.Combine(context.Variables[VariableNames.BuildSourcesDirectory], s);
                    buildTargets.AddRange(FindFile(searchPath));
                }
            }

            return buildTargets;
        }

         // Throws an Exception if the path appears to be a critical system folder (e.g. root of C:\ or /)
        public static void GuardPreventDamageToSystemFolders(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var root = Path.GetPathRoot(fullPath);
                if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Refusing to operate on critical system folder: {fullPath}");
                }

                // Throw an exception if the path is a parent of the root (e.g. C:\Windows\System32\..)
                if (fullPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase) 
                    || fullPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.System), StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Refusing to operate on critical system folder: {fullPath}");
                }

                // Prevent writing to Program Files or Program Files (x86)
                if (fullPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase) 
                    || fullPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Refusing to operate on critical system folder: {fullPath}");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Prevent writing to root or other critical system folders
                if (fullPath == "/" 
                    || fullPath == "/root" 
                    || fullPath == "/home" 
                    || fullPath == "/usr" 
                    || fullPath == "/bin" 
                    || fullPath == "/sbin"
                    || fullPath == "/etc" 
                    || fullPath == "/var")
                {
                    throw new ArgumentException($"Refusing to operate on critical system folder: {fullPath}");
                }

                // Prevent writing to /usr/local/bin or other common system paths
                if (fullPath.StartsWith("/usr/local/bin", StringComparison.Ordinal) 
                    || fullPath.StartsWith("/usr/bin", StringComparison.Ordinal) 
                    || fullPath.StartsWith("/usr/sbin", StringComparison.Ordinal) 
                    || fullPath.StartsWith("/bin", StringComparison.Ordinal) 
                    || fullPath.StartsWith("/sbin", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Refusing to operate on critical system folder: {fullPath}");
                }   
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system platform.");
            }
        }
    }

    public static class ExtensionMethods
    {
        public static void CopyTo(this DirectoryInfo directoryInfo, string destinationPath, bool overwrite = true)
        {
            FileUtils.GuardPreventDamageToSystemFolders(directoryInfo.FullName);
            FileUtils.GuardPreventDamageToSystemFolders(destinationPath);

            Parallel.ForEach(Directory.GetFileSystemEntries(directoryInfo.FullName, "*", SearchOption.AllDirectories), fileName => {
                var destFile = $"{destinationPath}{fileName[directoryInfo.FullName.Length..]}";
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                if (File.Exists(fileName)) File.Copy(fileName, destFile, overwrite);
            });
        }

        public static string ToPath(this string path) {
            return path.Replace('\\',Path.DirectorySeparatorChar).Replace('/',Path.DirectorySeparatorChar);
        }
    }
}
