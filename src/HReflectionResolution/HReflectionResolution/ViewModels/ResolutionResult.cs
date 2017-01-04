using System;
using System.IO;
using System.Security.Cryptography;

namespace HReflectionResolution.ViewModels
{
    [Serializable]
    public class ResolutionResult
    {
        public string Location { get; internal set; }
        public string Hash { get; set; }
        public long Size { get; set; }

        public ResolutionResult(string location)
        {
            Location = location;
            if (File.Exists(Location))
            {
                Hash = CalculateHash(Location);
                Size = CalculateSize(Location);
            }
        }

        public static long CalculateSize(string assemblyFilePath)
        {
            var fileInfo = new FileInfo(assemblyFilePath);
            if (fileInfo.Exists)
            {
                return fileInfo.Length;
            }

            return 0;
        }

        public static string CalculateHash(string assemblyFilePath)
        {
            if (!File.Exists(assemblyFilePath))
                return null;

            string hash = string.Empty;
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                hash = Convert.ToBase64String(cryptoProvider.ComputeHash(File.ReadAllBytes(assemblyFilePath)));
            }

            return hash;
        }
    }
}
