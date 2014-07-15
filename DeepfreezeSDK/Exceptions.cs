using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace DeepfreezeSDK
{
    public class Exceptions
    {
        public class DfApiException : Exception
        {
            public HttpResponseMessage HttpResponse;

            public DfApiException()
                : base() { }

            public DfApiException(string message, HttpResponseMessage response = null)
                : base(message)
            { HttpResponse = response; }

            public DfApiException(string message, Exception inner, HttpResponseMessage response = null)
                : base(message, inner)
            { HttpResponse = response; }
        }
    }
}
