using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

using Caliburn.Micro;
using DeepfreezeSDK;

namespace DeepfreezeApp
{
    [Export(typeof(IShell))]
    public class ShellViewModel : Screen, IShell
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeepfreezeClient _deepfreezeClient;

        private IArchiveViewModel _archiveVM = IoC.Get<IArchiveViewModel>();
        public ArchiveViewModel ArchiveVM
        {
            get { return this._archiveVM as ArchiveViewModel; }
        }

        private ILoginViewModel _loginVM = IoC.Get<ILoginViewModel>();
        public LoginViewModel LoginVM
        {
            get { return this._loginVM as LoginViewModel; }
        }

        public ShellViewModel() { }

        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, IDeepfreezeClient deepfreezeClient)
        {
            this._windowManager = windowManager;
            this._eventAggregator = eventAggregator;
            this._deepfreezeClient = deepfreezeClient;
        }
    }
}
