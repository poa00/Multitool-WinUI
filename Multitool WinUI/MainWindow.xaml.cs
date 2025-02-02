﻿using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

using Multitool.BL.Interop;
using Multitool.Data.Settings;
using Multitool.Data.Settings.Converters;

using MultitoolWinUI.Controls;
using MultitoolWinUI.Helpers;
using MultitoolWinUI.Pages;
using MultitoolWinUI.Pages.ControlPanels;
using MultitoolWinUI.Pages.Explorer;
using MultitoolWinUI.Pages.HashGenerator;
using MultitoolWinUI.Pages.MusicPlayer;
using MultitoolWinUI.Pages.Settings;
using MultitoolWinUI.Pages.Widgets;

using System;
using System.Diagnostics;

using Windows.Foundation;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MultitoolWinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const string fileName = "peepoPoopoo.gif";
        private static AppWindow thisWindow;
        private bool closed;
        private bool fullScreen;

        public MainWindow()
        {
            InitializeComponent();
            SetTitleBar();
            SizeChanged += MainWindow_SizeChanged;
            try
            {
                App.UserSettings.Load(this);
#if DEBUG
                // 
                if (LastPage == null)
                {
                    LastPage = typeof(MainPage);
                }
#endif
                WindowInteropHelper.SetWindow(this, WindowSize, new(PositionX, PositionY));
            }
            catch
            {
                Trace.TraceWarning("Failed to load main window settings");
            }
        }

        #region properties
        //[Setting(ElementTheme.Default)]
        public ElementTheme CurrentTheme { get; set; }

        [Setting(true)]
        public bool IsPaneOpen { get; set; }

        [Setting(typeof(TypeSettingConverter), HasDefaultValue = true, DefaultValue = typeof(MainPage))]
        public Type LastPage { get; set; }

        [Setting(0)]
        public int PositionX { get; set; }

        [Setting(0)]
        public int PositionY { get; set; }

        public int TitleBarHeight { get; private set; }

        [Setting(typeof(SizeSettingConverter), 1000, 600)]
        public Size WindowSize { get; set; } 
        #endregion

        public bool NavigateTo(Type pageType, params object[] navigationParameters)
        {
            try
            {
                if (pageType == null)
                {
                    return false;
                }
                return ContentFrame.Navigate(pageType, navigationParameters);
            }
            catch (Exception ex)
            {
                App.TraceError(ex);
                return false;
            }
        }

        private void NavigateTo(Type pageType, bool save)
        {
            try
            {
                if (pageType != null)
                {
                    if (save)
                    {
                        LastPage = pageType;
                    }
                    _ = ContentFrame.Navigate(pageType); 
                }
            }
            catch (Exception ex)
            {
                App.TraceError(ex);
            }
        }

        #region navigation events
        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            NavigateTo(LastPage, true);
            string tag = LastPage != null ? LastPage.Name : string.Empty;
            /*switch (LastPage.Name)
            {
                case nameof(MainPage):
                    tag = "home";
                    break;
                case nameof(ComputerDevicesPage):
                    tag = "devices";
                    break;
                case nameof(ExplorerPage):
                    tag = "explorer";
                    break;
                case nameof(ExplorerHomePage):
                    tag = "explorerhome";
                    break;
                case nameof(PowerPage):
                    tag = "power";
                    break;
                case nameof(ControlPanelsPage):
                    tag = "controlpanels";
                    break;
                case nameof(HashGeneratorPage):
                    tag = "hashgenerator";
                    break;
                case nameof(TwitchPage):
                    tag = "TwitchChat";
                    break;
                case nameof(SettingsPage):
                    tag = "Settings";
                    break;
                case nameof(MusicPlayerPage):
                    tag = "music";
                    break;
                default:
                    tag = string.Empty;
                    break;
            }*/
            var items = WindowNavigationView.MenuItems;
            foreach (var item in items)
            {
                if (item is NavigationViewItem itemBase && itemBase.Tag.ToString() == tag)
                {
                    WindowNavigationView.SelectedItem = itemBase;
                }
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                string tag = args.InvokedItemContainer.Tag.ToString();
                switch (tag)
                {
                    case nameof(MainPage):
                        NavigateTo(typeof(MainPage), true);
                        break;
                    case nameof(ComputerDevicesPage):
                        NavigateTo(typeof(ComputerDevicesPage), true);
                        break;
                    case nameof(ExplorerPage):
                        NavigateTo(typeof(ExplorerPage), true);
                        break;
                    case nameof(ExplorerHomePage):
                        NavigateTo(typeof(ExplorerHomePage), true);
                        break;
                    case nameof(ControlPanelsPage):
                        NavigateTo(typeof(ControlPanelsPage), true);
                        break;
                    case nameof(HashGeneratorPage):
                        NavigateTo(typeof(HashGeneratorPage), true);
                        break;
                    case nameof(TwitchPage):
                        NavigateTo(typeof(TwitchPage), true);
                        break;
                    case nameof(ChatPage):
                        NavigateTo(typeof(ChatPage), true);
                        break;
                    case nameof(WidgetsPage):
                        NavigateTo(typeof(WidgetsPage), true);
                        break;
                    case nameof(SettingsPage):
                    case "Settings":
                        NavigateTo(typeof(SettingsPage), true);
                        break;
                    case nameof(MusicPlayerPage):
                        NavigateTo(typeof(MusicPlayerPage), true);
                        break;
                    default:
                        App.TraceWarning("Page not found : " + tag);
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

        private void SetTitleBar()
        {
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);

            thisWindow = AppWindow.GetFromWindowId(windowId);
            thisWindow.Changed += AppWindow_Changed;

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                thisWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                thisWindow.TitleBar.ButtonBackgroundColor = Tool.GetAppRessource<Color>("DarkBlack");
                thisWindow.TitleBar.ButtonForegroundColor = Colors.White;
                thisWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                thisWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                //ColorConverter.ConvertFromString("#968677", 122)
                thisWindow.TitleBar.ButtonHoverBackgroundColor = Tool.GetAppRessource<Color>("AppTitleBarHoverColor");
                thisWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                thisWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;
                thisWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;

                Uri imageSource = new(@$"ms-appx:///Resources/Images/{fileName}");
                WindowIcon.Source = new BitmapImage(imageSource); 
            }
            else
            {
                TitleBarGrid.Visibility = Visibility.Collapsed;
                var row = ContentGrid.RowDefinitions[0];
                row.MinHeight = 0;
                row.Height = new(0);
            }
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (thisWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                fullScreen = overlappedPresenter.State == OverlappedPresenterState.Maximized;

                if (args.DidPositionChange && !fullScreen)
                {
                    PositionX = thisWindow.Position.X;
                    PositionY = thisWindow.Position.Y;
                }
            }
        }

        #region window events
        private void PresenterModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (thisWindow.Presenter.Kind != AppWindowPresenterKind.CompactOverlay)
            {
                thisWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
            }
            else
            {
                thisWindow.SetPresenter(AppWindowPresenterKind.Default);
            }
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (!closed && !fullScreen)
            {
                WindowSize = args.Size;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // save settings
            closed = true;
            MessageDisplay.Silence();
            try
            {
                App.UserSettings.Save(this);
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError(ex.ToString());
            }
            catch { }
        }

        private void MessageDisplay_VisibilityChanged(AppMessageControl sender, Visibility args)
        {
            if (!closed)
            {
                ContentPopup.IsOpen = args == Visibility.Visible;
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            
        }
        #endregion
    }
}
