﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace DeepfreezeApp
{
    [Export(typeof(IRefreshUserMessage))]
    public class RefreshUserMessage : IRefreshUserMessage
    {
    }
}