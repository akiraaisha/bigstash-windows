using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeSDK
{
    static class HelperMethods
    {
        /// <summary>
        /// Returns the sum of long numbers found in an enumerable collection.
        /// </summary>
        /// <param name="numbers"></param>
        /// <returns></returns>
        public static long SumNumbers(IEnumerable<long> numbers)
        {
            long sum = 0;
            foreach(long num in numbers)
            {
                sum += num;
            }
            return sum;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static Enumber
    }
}
