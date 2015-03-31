using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace BigStash.WPF.Messages
{
    [Export(typeof(ICreateArchiveMessage))]
    public class CreateArchiveMessage : ICreateArchiveMessage
    {
        private IEnumerable<string> _paths;

        public IEnumerable<string> Paths
        {
            get { return this._paths; }
            set { this._paths = value; }
        }
    }
}
