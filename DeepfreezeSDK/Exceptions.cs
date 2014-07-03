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

        public class NoArchivesFoundException : Exception
        {
            public NoArchivesFoundException()
                : base("Could not fetch any archives.") { }

            public NoArchivesFoundException(string message)
                : base(message)
            { }

            public NoArchivesFoundException(string message, Exception inner)
                : base(message, inner)
            { }
        }

        public class NoUploadsFoundException : Exception
        {
            public NoUploadsFoundException()
                : base("Could not fetch any archives.") { }

            public NoUploadsFoundException(string message)
                : base(message)
            { }

            public NoUploadsFoundException(string message, Exception inner)
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

        public class CreateArchiveException : Exception
        {
            public CreateArchiveException()
                : base("Could not create a new archive.") { }

            public CreateArchiveException(string message)
                : base(message)
            { }

            public CreateArchiveException(string message, Exception inner)
                : base(message, inner)
            { }
        }

        public class CreateUploadException : Exception
        {
            public CreateUploadException()
                : base("Could not create a new upload.") { }

            public CreateUploadException(string message)
                : base(message)
            { }

            public CreateUploadException(string message, Exception inner)
                : base(message, inner)
            { }
        }
    }
}
