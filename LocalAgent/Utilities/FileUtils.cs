using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace LocalAgent.Utilities
{
    public class FileUtils
    {
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

        /// <summary>
        /// Copies all the files from one folder into another, in parallel
        /// </summary>
        /// <param name="sourcePath">Folder content to copy</param>
        /// <param name="destinationPath">Folder destination to receive content</param>
        public void CloneFolder(string sourcePath, string destinationPath)
        {
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
            var info = new DirectoryInfo(path);
            if (!info.Exists)
            {
                throw new ArgumentException($"Folder not found: '{path}'", nameof(path));
            }

            info.Delete(true);
            info.Create();
        }

        // Creates a folder, and subfolders
        public void CreateFolder(string path)
        {
            Directory.CreateDirectory(path);
        }
    }

    public static class ExtensionMethods
    {
        public static void CopyTo(this DirectoryInfo directoryInfo, string destinationPath, bool overwrite = true)
        {
            Parallel.ForEach(Directory.GetFileSystemEntries(directoryInfo.FullName, "*", SearchOption.AllDirectories), fileName => {
                var destFile = $"{destinationPath}{fileName[directoryInfo.FullName.Length..]}";
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                if (File.Exists(fileName)) File.Copy(fileName, destFile, overwrite);
            });
        }

    }
}
