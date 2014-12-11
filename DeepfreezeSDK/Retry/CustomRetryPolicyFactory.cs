using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace DeepfreezeSDK.Retry
{
    public static class CustomRetryPolicyFactory
    {
        /// <summary>
        /// Create an Exponential Backoff RetryPolicy using 
        /// a HttpTransientErrorDetectionStrategy detection strategy.
        /// </summary>
        /// <returns></returns>
        public static RetryPolicy MakeHttpRetryPolicy(int retryCount)
        {
            var strategy = new HttpTransientErrorDetectionStrategy();
            return Exponential(strategy, retryCount);
        }

        /// <summary>
        /// Create an exponential backoff retry policy given a detection strategy.
        /// </summary>
        /// <param name="strategy"></param>
        /// <returns></returns>
        private static RetryPolicy Exponential(ITransientErrorDetectionStrategy strategy, int retryCount)
        {
            if (retryCount == 0)
                return RetryPolicy.NoRetry;

            if (retryCount == 1)
            {
                var retryPolicy = new RetryPolicy(strategy, 1);
                retryPolicy.RetryStrategy.FastFirstRetry = true;

                return retryPolicy;
            }
            
            var minBackoff = TimeSpan.FromSeconds(1);
            var maxBackoff = TimeSpan.FromSeconds(10);
            var deltaBackoff = TimeSpan.FromSeconds(5);

            // 30 60 120 240

            var exponentialBackoff = new ExponentialBackoff(retryCount, minBackoff, maxBackoff, deltaBackoff);

            return new RetryPolicy(strategy, exponentialBackoff);
        }
    }
}
