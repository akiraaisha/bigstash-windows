﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace BigStash.WPF
{
    [Export(typeof(ILoginSuccessMessage))]
    public class LoginSuccessMessage : ILoginSuccessMessage
    {
    }
}
