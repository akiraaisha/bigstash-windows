using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DeepfreezeModel;

namespace BigStash.WPF
{
    public interface INotificationViewModel
    {
        Notification Notification { get; set; }

        bool IsNew { get; set; }
    }
}
