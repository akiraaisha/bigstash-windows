﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace BigStash.WPF
{
    [Export(typeof(IRestartNeededMessage))]
    public class RestartNeededMessage : IRestartNeededMessage
    {
        public bool RestartNeeded { get; set; }
    }
}
