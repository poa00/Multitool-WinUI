﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Multitool.DAL;

using MultitoolWinUI.Helpers;
using MultitoolWinUI.Pages;
using MultitoolWinUI.Pages.ControlPanels;
using MultitoolWinUI.Pages.Explorer;
using MultitoolWinUI.Pages.HashGenerator;
using MultitoolWinUI.Pages.Power;

using System;
using System.Diagnostics;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using MultitoolWinUI.Controls;
using MultitoolWinUI.Pages.Test;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MultitoolWinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private Type lastPage;
        private bool closed;

        public MainWindow()
        {
            InitializeComponent();
            Title = "Multitool v." + Tool.GetPackageVersion();
            SizeChanged += MainWindow_SizeChanged;

            try
            {
                IsPaneOpen = App.Settings.GetSetting<bool>(nameof(MainWindow), nameof(IsPaneOpen));
            }
            catch (SettingNotFoundException)
            {
                IsPaneOpen = true;
            }

            try
            {
                string lastPageName = App.Settings.GetSetting<string>(nameof(MainWindow), nameof(lastPage));
                switch (lastPageName)
                {
                    case nameof(MainPage):
                        lastPage = typeof(MainPage);
                        break;
                    case nameof(ComputerDevicesPage):
                        lastPage = typeof(ComputerDevicesPage);
                        break;
                    case nameof(ExplorerPage):
                        lastPage = typeof(ExplorerPage);
                        break;
                    case nameof(ExplorerHomePage):
                        lastPage = typeof(ExplorerHomePage);
                        break;
                    case nameof(PowerPage):
                        lastPage = typeof(PowerPage);
                        break;
                    case nameof(ControlPanelsPage):
                        lastPage = typeof(ControlPanelsPage);
                        break;
                    case nameof(HashGeneratorPage):
                        lastPage = typeof(HashGeneratorPage);
                        break;
                    case nameof(IrcChatPage):
                        lastPage = typeof(IrcChatPage);
                        break;
                    default:
                        Trace.TraceWarning("MainWindow:ctor: Unknown page");
                        break;
                }
            }
            catch (SettingNotFoundException) { }
#if false
            Trace.Listeners[0] = new WindowTrace(this) { Name = "Debug_WindowTrace" };
#else
            Trace.Listeners.Add(new WindowTrace(this) { Name = "WindowTrace" });
#endif
        }

        public bool IsPaneOpen { get; set; }

        #region navigation events
        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            if (lastPage != null)
            {
                _ = ContentFrame.Navigate(lastPage);
            }
            else
            {
                lastPage = typeof(MainPage);
                _ = ContentFrame.Navigate(typeof(MainPage));
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                string tag = args.InvokedItemContainer.Tag.ToString();
                switch (tag)
                {
                    case "home":
                        lastPage = typeof(MainPage);
                        _ = ContentFrame.Navigate(typeof(MainPage));
                        break;
                    case "devices":
                        lastPage = typeof(ComputerDevicesPage);
                        _ = ContentFrame.Navigate(typeof(ComputerDevicesPage));
                        break;
                    case "explorer":
                        lastPage = typeof(ExplorerPage);
                        _ = ContentFrame.Navigate(typeof(ExplorerPage));
                        break;
                    case "explorerhome":
                        lastPage = typeof(ExplorerHomePage);
                        _ = ContentFrame.Navigate(typeof(ExplorerHomePage));
                        break;
                    case "power":
                        lastPage = typeof(PowerPage);
                        _ = ContentFrame.Navigate(typeof(PowerPage));
                        break;
                    case "controlpanels":
                        lastPage = typeof(ControlPanelsPage);
                        _ = ContentFrame.Navigate(typeof(ControlPanelsPage));
                        break;
                    case "hashgenerator":
                        lastPage = typeof(HashGeneratorPage);
                        _ = ContentFrame.Navigate(typeof(HashGeneratorPage));
                        break;
                    case "irc":
                        lastPage = typeof(IrcChatPage);
                        _ = ContentFrame.Navigate(typeof(IrcChatPage));
                        break;
                    case "test":
                        lastPage = typeof(TestPage);
                        _ = ContentFrame.Navigate(typeof(TestPage));
                        break;
                    default:
                        Trace.TraceWarning("Trying to navigate to : " + tag);
                        break;
                }
            }
        }

        private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }
        #endregion

        #region window events
        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            _ = DispatcherQueue.TryEnqueue(() => MessageDisplay.Width = args.Size.Width);
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // save settings
            closed = true;
            IEnumerable<WindowTrace> tracers = Trace.Listeners.OfType<WindowTrace>();
            for (int i = 0; i < Trace.Listeners.Count; i++)
            {
                if (Trace.Listeners[i].GetType().IsAssignableFrom(typeof(WindowTrace)))
                {
                    Trace.Listeners.RemoveAt(i);
                }
            }
            if (lastPage != null)
            {
                App.Settings.SaveSetting(nameof(MainWindow), nameof(lastPage), lastPage.Name);
            }
            App.Settings.SaveSetting(nameof(MainWindow), nameof(IsPaneOpen), IsPaneOpen);
        }

        private void MessageDisplay_VisibilityChanged(TraceControl sender, Visibility args)
        {
            if (!closed)
            {
                ContentPopup.IsOpen = args == Visibility.Visible;
            }
        }

        #endregion
    }
}
