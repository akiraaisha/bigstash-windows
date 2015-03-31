using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigStash.SDK.Exceptions
{
    public static class BigStashExceptionHelper
    {
        /// <summary>
        /// Gets the request body of the sent request which resulted in the BigStashException throw.
        /// </summary>
        /// <param name="bgex"></param>
        /// <returns></returns>
        public static string TryGetBigStashExceptionInformation(Exception ex)
        {
            if (ex is BigStashException)
            {
                var bgex = ex as BigStashException;
                var request = bgex.Request;

                if (request != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine();
                    sb.AppendLine("Error Type: " + bgex.ErrorType);
                    sb.AppendLine("Error Code: " + bgex.ErrorCode);
                    sb.AppendLine("Status Code: " + bgex.StatusCode);
                    sb.AppendLine("Failed Request:");
                    sb.Append("    " + request.ToString().Replace(Environment.NewLine, Environment.NewLine + "        "));

                    return sb.ToString();
                }

                return "";
            }

            return "";
        }
    }
}
