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
            Pausing,
            Paused,
            Cancelling,
            Cancelled,
            Failed,
            Aborted
        }

        public static Status GetStatusFromString(string statusString)
        {
            Status status = Status.Failed;

            switch(statusString)
            {
                case "creating":
                    status = Status.Creating;
                    break;
            }

            return status;
        }
    }
}
