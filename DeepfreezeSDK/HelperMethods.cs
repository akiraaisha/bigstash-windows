using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeSDK
{
    static class HelperMethods
    {
        /// <summary>
        /// Encode plain text string to base64.
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns>string</returns>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.ASCII.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Decode base64 string to plain text string.
        /// </summary>
        /// <param name="base64EncodedData"></param>
        /// <returns>string</returns>
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.ASCII.GetString(base64EncodedBytes);
        }

        /// <summary>
        /// Convert byte array to base64 string.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns>string</returns>
        public static string ByteArrayToBase64String(byte[] hash)
        {
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Convert string to byte array using ASCII encoding.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static byte[] StringASCIIToByteArray(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        /// <summary>
        /// Sign a string using a secret string using the HMACSHA256 method.
        /// This method converts the given strings to byte arrays and returns
        /// a hashed string encoded in base64.
        /// </summary>
        /// <param name="secret"></param>
        /// <param name="data"></param>
        /// <returns>string</returns>
        public static string HMACSHA256Sign(string secret, string data)
        {
            byte[] key = HelperMethods.StringASCIIToByteArray(secret);
            byte[] input = HelperMethods.StringASCIIToByteArray(data);

            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                // Compute the hash of the data string. 
                byte[] hashValue = hmac.ComputeHash(input);

                return HelperMethods.ByteArrayToBase64String(hashValue);
            }
        }
    }
}
