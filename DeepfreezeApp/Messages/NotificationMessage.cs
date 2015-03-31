using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using BigStash.Model;

namespace BigStash.WPF
{
    [Export(typeof(INotificationMessage))]
    public class NotificationMessage : INotificationMessage
    {
        public string Message { get; set; }

        public Enumerations.NotificationStatus NotificationStatus { get; set; }
    }
}
