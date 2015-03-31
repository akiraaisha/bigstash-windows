using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigStash.SDK.Exceptions
{
    public class BigStashException : Exception
    {
        #region properties

        public System.Net.Http.HttpRequestMessage Request { get; set; }
        public ErrorType ErrorType { get; set; }
        public ErrorCode ErrorCode { get; set; }
        public System.Net.HttpStatusCode StatusCode { get; set; }
        public int CurrentRetryCount { get; set; }

        #endregion

        public BigStashException(ErrorType errorType = ErrorType.NotSet,
                                        ErrorCode errorCode = ErrorCode.NotSet)
            : base() { this.ErrorType = errorType; this.ErrorCode = errorCode; }

        public BigStashException(string message,
                                        ErrorType errorType = ErrorType.NotSet,
                                        ErrorCode errorCode = ErrorCode.NotSet)
            : base(message) { this.ErrorType = errorType; this.ErrorCode = errorCode; }

        public BigStashException(string message,
                                        Exception inner,
                                        ErrorType errorType = ErrorType.NotSet,
                                        ErrorCode errorCode = ErrorCode.NotSet)
            : base(message, inner) { this.ErrorType = errorType; this.ErrorCode = errorCode; }

        #region public_methods

        /// <summary>
        /// Returns the InnerExceptions read-only collection, if InnerException is of type AggregateException.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyCollection<Exception> TryGetInnerExceptions()
        {
            if (this.InnerException is AggregateException)
            {
                return ((AggregateException)this.InnerException).InnerExceptions;
            }
            else
                return null;
        }

        #endregion
    }

    /// <summary>
    /// ErrorType defines the source of the exception.
    /// Example: Failing to deserialize a json response is the client's fault,
    /// but trying to delete a client upload that doesn't exist in the BigStash servers
    /// is a service exception.
    /// </summary>
    public enum ErrorType
    {
        NotSet,
        Service,
        Client,
        Unknown
    }

    public enum ErrorCode
    {
        NotSet
    }
}
