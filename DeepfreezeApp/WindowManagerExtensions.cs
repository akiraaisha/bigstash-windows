﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Dynamic;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;

namespace DeepfreezeApp
{
    public static class WindowManagerExtensions
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(WindowManagerExtensions));

        public static async Task<MessageBoxResult> ShowMessageViewModelAsync(this IWindowManager @this, string message, string title,
                                                           MessageBoxButton buttons)
        {
            MessageBoxResult retval = MessageBoxResult.Cancel;
            var shellViewModel = IoC.Get<IShell>();

            try
            {
                var model = new MessageBoxViewModel(message, title, buttons);

                dynamic settings = new ExpandoObject();
                settings.Width = shellViewModel.ShellWindow.ActualWidth;
                await shellViewModel.ShellWindow.ShowOverlayAsync();
                @this.ShowDialog(model, null, settings);
                await shellViewModel.ShellWindow.HideOverlayAsync();
                retval = model.Result;
            }
            catch (Exception e) 
            {
                _log.Error("WindowManagerExtensions.ShowMessageViewModelAsync threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
            finally
            {
            }

            return retval;
        }

        public static async Task ShowViewDialogAsync(this IWindowManager @this, object viewModel)
        {
            var shellViewModel = IoC.Get<IShell>();

            try
            {
                dynamic settings = new ExpandoObject();
                settings.Width = shellViewModel.ShellWindow.ActualWidth;
                await shellViewModel.ShellWindow.ShowOverlayAsync();
                @this.ShowDialog(viewModel, null, settings);
                await shellViewModel.ShellWindow.HideOverlayAsync();
            }
            catch (Exception e) 
            {
                _log.Error("WindowManagerExtensions.ShowViewDialogAsync threw " + e.GetType().ToString() + " with message \"" + e.Message + "\".");
            }
        }
    }
}
