using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeSDK
{
    public class Exceptions
    {
        public class NoActiveTokenException : Exception
        {
            public NoActiveTokenException()
                : base("No active tokens found. Please login with your Deepfreeze acount.") { }

            public NoActiveTokenException(string message)
                : base(message)
            { }

            public NoActiveTokenException(string message, Exception inner) 
                : base(message, inner)
            { }
        }

        public class CreateTokenException : Exception
        {
            public CreateTokenException()
                : base("Could not create a new token.") { }

            public CreateTokenException(string message)
                : base(message)
            { }

            public CreateTokenException(string message, Exception inner)
                : base(message, inner)
            { }
        }
    }
}
