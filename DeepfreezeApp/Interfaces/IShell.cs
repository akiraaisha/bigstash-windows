using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MahApps.Metro.Controls;

namespace DeepfreezeApp
{
    public interface IShell
    {
        MetroWindow ShellWindow { get; }
    }
}
