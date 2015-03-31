using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BigStash.Model;

namespace BigStash.WPF
{
    public interface INotificationMessage
    {
        string Message { get; set; }
        Enumerations.NotificationStatus NotificationStatus { get; set; }
    }
}
