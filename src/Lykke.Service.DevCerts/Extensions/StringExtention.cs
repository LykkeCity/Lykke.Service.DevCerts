using System;
using System.Security.Cryptography;
using System.Text;

namespace Lykke.Service.DevCerts.Extensions
{
    public static class StringExtention
    {
        public static string GetHash(this string str)
        {
            var hash = BitConverter.ToString(Hash(str)).Replace("-", string.Empty);
            return hash;
        }

        private static byte[] Hash(string value)
        {
            return Hash(Encoding.UTF8.GetBytes(value));
        }

        private static byte[] Hash(byte[] value)
        {
            using (var sha256 = SHA256.Create())
            {
                // Send a sample text to hash.  
                return sha256.ComputeHash(value);
                // Get the hashed string.  
            }

        }
    }
}
