﻿using Microsoft.UI.Xaml.Controls;

using Multitool.NTInterop;
using Multitool.NTInterop.Power;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Timers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MultitoolWinUI.Pages.Power
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PowerPage : Page, INotifyPropertyChanged
    {
        private PowerController controller = new();
        private bool _buttonsEnabled;

        public PowerPage()
        {
            InitializeComponent();
        }

        #region properties

        public bool ButtonsEnabled
        {
            get => _buttonsEnabled;
            set
            {
                _buttonsEnabled = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region controller methods

        private void Shutdown()
        {
            try
            {
                controller.Shutdown();
            }
            catch (OperationFailedException ex)
            {
                Trace.WriteLine(ex.ToString());
                App.MainWindow.DisplayMessage("Unable to shutdown the system. The operation failed.");
            }
        }

        private void Restart()
        {
            try
            {
                controller.Restart();
            }
            catch (OperationFailedException ex)
            {
                Trace.WriteLine(ex.ToString());
                App.MainWindow.DisplayMessage("Unable to restart the system. The operation failed");
            }
        }

        private void Lock()
        {
            try
            {
                controller.Lock();
            }
            catch (OperationFailedException ex)
            {
                Trace.WriteLine(ex.ToString()); App.MainWindow.DisplayMessage("Unable to lock the system. The operation failed"); 
            }
        }

        private void Sleep()
        {
            try
            {
                controller.Suspend();
            }
            catch (OperationFailedException ex)
            {
                Trace.WriteLine(ex.ToString()); App.MainWindow.DisplayMessage("Unable to suspend the system. The operation failed"); 
            }
        }

        private void Hibernate()
        {
            try
            {
                controller.Hibernate();
            }
            catch (OperationFailedException ex)
            {
                Trace.WriteLine(ex.ToString()); App.MainWindow.DisplayMessage("Unable to hibernate the system. The operation failed");
            }
        }

        #endregion

        #region window methods

        private void NotifyPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region events

        private void TimerPicker_Elapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                switch ((SelectionComboBox.SelectedItem as ComboBoxItem).Tag)
                {
                    case "lock":
                        Lock();
                        break;
                    case "sleep":
                        Sleep();
                        break;
                    case "hiber":
                        Hibernate();
                        break;
                    case "shut":
                        Shutdown();
                        break;
                    case "restart":
                        Restart();
                        break;
                }
            });
#endif
        }

        private void TimerPicker_StatusChanged(Controls.TimerPicker sender, bool args)
        {

        }

        #endregion
    }
}
