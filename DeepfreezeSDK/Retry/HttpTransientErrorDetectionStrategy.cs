using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using DeepfreezeSDK.Exceptions;

namespace DeepfreezeSDK.Retry
{
    public class HttpTransientErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        /// <summary>
        /// Check if an exception is the result of a transient service fault.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public bool IsTransient(Exception ex)
        {
            if (ex != null)
            {
                BigStashException bigStashException;

                if ((bigStashException = ex as BigStashException) != null)
                {
                    if (HelperMethods.IsFailedStatusCodeServiceFault(bigStashException.StatusCode) == ErrorType.Service)
                    {
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }
    }
}
