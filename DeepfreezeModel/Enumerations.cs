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
            Creating,
            Pending,
            Resuming,
            InProgress,
            Uploading,
            Pausing,
            Paused,
            Uploaded,
            Completed,
            Frozen,
            Cancelling,
            Cancelled,
            Failed,
            Aborted,
            UnableToStart
        }

        public static Status GetStatusFromString(string statusString)
        {
            Status status = Status.Failed;

            switch(statusString)
            {
                case "creating":
                    status = Status.Creating;
                    break;
                case "frozen":
                    status = Status.Frozen;
                    break;
                case "uploaded":
                    status = Status.Uploaded;
                    break;
                case "completed":
                    status = Status.Completed;
                    break;
                case "failed":
                    status = Status.Failed;
                    break;
                case "paused":
                    status = Status.Paused;
                    break;
                case "uploading":
                    status = Status.Uploading;
                    break;
            }

            return status;
        }
    }
}
