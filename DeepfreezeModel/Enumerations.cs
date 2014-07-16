using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeModel
{
    public class Enumerations
    {
        public enum Status
        {
            Pending,
            Uploaded,
            Error,
            Completed,
            Paused,
            Uploading
        }

        public static Status GetStatusFromString(string statusString)
        {
            Status status = Status.Error;

            switch(statusString)
            {
                case "uploaded":
                    status = Status.Uploaded;
                    break;
                case "completed":
                    status = Status.Completed;
                    break;
                case "paused":
                    status = Status.Paused;
                    break;
                case "error":
                    status = Status.Error;
                    break;
                case "pending":
                    status = Status.Paused;
                    break;
            }

            return status;
        }

        public enum UploadAction
        {
            Create,
            Start,
            Pause
        }
    }
}
