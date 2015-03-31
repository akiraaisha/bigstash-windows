using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace BigStash.WPF
{
    [Export(typeof(IFetchNotificationsMessage))]
    public class FetchNotificationsMessage : IFetchNotificationsMessage
    {
        public int? PagedResult { get; set; }
    }
}
