using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;

namespace LocalAgent.Utilities
{
    public class FileDetails
    {
        public static byte[] GetMd5HashBytes(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            return md5.ComputeHash(stream);
        }

        public static string GetMd5Hash(string filePath)
        {
            return BitConverter.ToString(GetMd5HashBytes(filePath))
                .Replace("-", "");
        }

        public static bool TestFile(FileInfo fileInfo, string extension)
        {
            return fileInfo.Exists
                   && fileInfo.Extension.ToLower() == ".yml";
        }
    }
}
