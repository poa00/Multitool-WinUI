﻿using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Multitool.Data;
using Multitool.Data.Completion;
using Multitool.Data.FileSystem;
using Multitool.Data.FileSystem.Events;
using Multitool.Data.Settings;
using Multitool.Parsers;
using Multitool.Sorting;
using Multitool.Threading;

using MultitoolWinUI.Controls;
using MultitoolWinUI.Helpers;
using MultitoolWinUI.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

using FileAttributes = System.IO.FileAttributes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MultitoolWinUI.Pages.Explorer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ExplorerPage : Page, INotifyPropertyChanged
    {
        private static readonly SolidColorBrush RED = new(Colors.Red);
        private static readonly SolidColorBrush WHITE = new(Colors.White);

        private readonly IFileSystemManager fileSystemManager = new FileSystemManager() { Notify = true };
        private readonly Stopwatch managerEventStopwatch = new();
        private readonly IPathCompletor pathCompletor = new PathCompletor();
        private readonly Stopwatch sortEventStopwatch = new();
        private readonly Stack<string> previousStackPath = new(10);
        private readonly Stack<string> nextPathStack = new(10);
        private readonly Stopwatch taskStopwatch = new();
        private readonly object sortingLock = new();

        private ListenableCancellationTokenSource fsCancellationTokenSource;
        private string _currentPath;

        public ExplorerPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            App.MainWindow.Closed += OnMainWindowClosed;
            fileSystemManager.Progress += FileSystemManager_Progress;
            fileSystemManager.Exception += FileSystemManager_Exception;
            fileSystemManager.Changed += FileSystemManager_Change;

            try
            {
                App.UserSettings.Load(this);
                foreach (var item in History)
                {
                    item.DispatcherQueue = DispatcherQueue;
                }
            }
            catch (SettingNotFoundException ex)
            {
                App.TraceError(ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region properties
        [Setting("C:\\")]
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                _currentPath = value;
                PropertyChanged?.Invoke(this, new(nameof(CurrentPath)));
            }
        }

        public ObservableCollection<FileSystemEntryView> CurrentFiles { get; } = new();

        [Setting(typeof(PathHistoryItemSettingConverter))]
        public ObservableCollection<PathHistoryItem> History { get; set; }

        [Setting(true)]
        public bool KeepHistory { get; set; }

        [Setting(true)]
        public bool LoadLastPath { get; set; }
        #endregion

        #region private methods
        private void DisplayFiles(string path)
        {
            if (fsCancellationTokenSource != null)
            {
                async void a(ListenableCancellationTokenSource sender, EventArgs args)
                {
                    await LoadFiles(path);
                    fsCancellationTokenSource.Cancelled -= a;
                }
                fsCancellationTokenSource.Cancelled += a;
                CancelFileTask();
            }
            else
            {
                fsCancellationTokenSource = new();
                _ = LoadFiles(path);
            }
        }

        private async Task LoadFiles(string path)
        {
            try
            {
                string realPath = fileSystemManager.GetRealPath(UriCleaner.RemoveChariotReturns(path));
                bool contains = false;
                foreach (var item in History)
                {
                    if (item.FullPath == realPath)
                    {
                        contains = true;
                    }
                }
                if (!contains)
                {
                    string shortPath = Path.GetFileName(realPath);
                    if (string.IsNullOrEmpty(shortPath))
                    {
                        shortPath = realPath;
                    }
                    History.Add(new(DispatcherQueue)
                    {
                        FullPath = realPath,
                        ShortPath = shortPath
                    });
                }
                CurrentPath = realPath;
                CurrentFiles.Clear();
                progress_TextBox.Text = string.Empty;

                files_ProgressBar.IsIndeterminate = true;
                cancelAction_Button.IsEnabled = true;

                managerEventStopwatch.Restart();
                sortEventStopwatch.Restart();
                taskStopwatch.Restart();
                try
                {
                    await fileSystemManager.GetEntries(realPath, CurrentFiles, AddDelegate, fsCancellationTokenSource.Token);

                    taskStopwatch.Stop();
                    fsCancellationTokenSource.Dispose();
                    fsCancellationTokenSource = null;

                    taskStopwatch.Stop();
                    managerEventStopwatch.Reset();
                    sortEventStopwatch.Reset();

                    cancelAction_Button.IsEnabled = false;
                    SortList();
                    files_ProgressBar.IsIndeterminate = false;
                    progress_TextBox.Foreground = new SolidColorBrush(Colors.White);
                    if (taskStopwatch.Elapsed.TotalSeconds < 1)
                    {
                        progress_TextBox.Text = "Task successfully completed (in " + taskStopwatch.ElapsedMilliseconds.ToString() + "ms)";
                    }
                    else
                    {
                        TimeSpan elapsed = taskStopwatch.Elapsed;
                        progress_TextBox.Text = "Task successfully completed (in " + elapsed.ToString("mm\\:ss") + ")";
                    }

                    for (int i = 0; i < CurrentFiles.Count; i++)
                    {
                        FileSystemEntryView item = CurrentFiles[i];
                        if (item.IsDirectory && !Directory.Exists(item.Path))
                        {
                            _ = DispatcherQueue.TryEnqueue(() => item.Color = new SolidColorBrush(Colors.Red));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    managerEventStopwatch.Reset();
                    cancelAction_Button.IsEnabled = false;
                    files_ProgressBar.IsIndeterminate = false;

                    fsCancellationTokenSource.InvokeCancel();
                    fsCancellationTokenSource = null;
                    App.TraceWarning("Operation cancelled, loading path : " + path);
                    progress_TextBox.Text = "Operation cancelled";
                    DispatcherQueue.TryEnqueue(() => SortList());
                }
                catch (Exception ex) // we catch everything, and display it to the trace and UI
                {
                    managerEventStopwatch.Reset();
                    cancelAction_Button.IsEnabled = false;
                    files_ProgressBar.IsIndeterminate = false;

                    App.TraceError(ex);
                    progress_TextBox.Text = ex.ToString();
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                fsCancellationTokenSource = null;
                managerEventStopwatch.Reset();
                cancelAction_Button.IsEnabled = false;
                files_ProgressBar.IsIndeterminate = false;

                App.TraceError(ex);
                CurrentPath = string.Empty;
                progress_TextBox.Text = "Directory not found : '" + path + "'";
            }
        }

        private void AddDelegate(IList<FileSystemEntryView> items, IFileSystemEntry item)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                FileSystemEntryView view = new(item)
                {
                    ListView = mainListView,
                    Page = this
                };
                view.SizedChanged += View_SizeChanged;
                items.Add(view);
            });
        }

        private void SortList()
        {
            lock (sortingLock)
            {
                FileSystemEntryView[] pathItems = new FileSystemEntryView[CurrentFiles.Count];
                CurrentFiles.CopyTo(pathItems, 0);
                QuickSort.Sort(pathItems, 0, pathItems.Length - 1);

                bool notSame = false;
                for (int i = 0; i < pathItems.Length; i++)
                {
                    if (CurrentFiles[i].Path != pathItems[i].Path)
                    {
                        notSame = true;
                        break;
                    }
                }
                if (notSame)
                {
                    for (int i = 0; i < CurrentFiles.Count; i++)
                    {
                        CurrentFiles[i] = null;
                    }
                    for (int i = 0; i < pathItems.Length; i++)
                    {
                        CurrentFiles[i] = pathItems[i];
                    }
                }
            }
        }

        private void CancelFileTask()
        {
            if (fsCancellationTokenSource != null)
            {
                fsCancellationTokenSource.Cancel();
                fsCancellationTokenSource.Dispose();
                fsCancellationTokenSource = new();
            }
        }

        private void DisplayMessage(string message, bool error = false, bool force = false)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (force || managerEventStopwatch.ElapsedMilliseconds > 100) //ms interval between each notification
                {
                    if (error)
                    {
                        progress_TextBox.Foreground = RED;
                        progress_TextBox.Text = message;
                    }
                    else
                    {
                        progress_TextBox.Foreground = WHITE;
                        progress_TextBox.Text = message;
                    }
                    managerEventStopwatch.Restart();
                }
            });
        }

        private void Next()
        {
            if (nextPathStack.Count > 0)
            {
                previousStackPath.Push(CurrentPath);
                DisplayFiles(nextPathStack.Pop());
            }
        }

        private void Back()
        {
            if (previousStackPath.Count > 0)
            {
                nextPathStack.Push(CurrentPath);
                DisplayFiles(previousStackPath.Pop());
            }
            else
            {
                DirectoryInfo info = Directory.GetParent(CurrentPath);
                if (info != null)
                {
                    nextPathStack.Push(CurrentPath);
                    DisplayFiles(info.FullName);
                }
            }
        }

        private void Refresh()
        {
            fileSystemManager.Reset();
            DisplayFiles(CurrentPath);
        }

        private void CompletePath()
        {
            string path = pathInput.Text;
            try
            {
                Regex regex = new(@"^[A-z]:(\\|\/).*", RegexOptions.IgnoreCase & RegexOptions.Compiled);
                if (!regex.IsMatch(path))
                {
                    List<string> items = new();
                    DriveInfo[] driveInfos = DriveInfo.GetDrives();
                    for (int i = 0; i < driveInfos.Length; i++)
                    {
                        items.Add(driveInfos[i].Name);
                    }
                    pathInput.ItemsSource = items;
                }
                else
                {
                    pathInput.ItemsSource = pathCompletor.Complete(path);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Trace.TraceError(e.ToString());
            }
        }

        private void SavePage()
        {
            App.UserSettings.Save(this);
        }
        #endregion

        #region events

        #region navigation events
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string path = e.Parameter as string;
            if (!string.IsNullOrEmpty(path))
            {
                DisplayFiles(path);
            }
        }
        #endregion

        #region window events
        private void MainListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is IFileSystemEntry item)
                {
                    //FileSystemSecurity fss = item.GetAccessControl();
#if false
                    BottomBarAccessRightType.Inlines.Add(new Run() { Text = fss.AccessRightType.Name });
                    //BottomBarAccessRuleModified = fss.AccessRules;
                    BottomBarAccessRuleType.Inlines.Add(new Run() { Text = fss.AccessRuleType.Name });
                    BottomBarAreAccessRulesCanonical.Inlines.Add(new Run() { Text = fss.AreAccessRulesCanonical.ToString() });
                    BottomBarAreAccessRulesProtected.Inlines.Add(new Run() { Text = fss.AreAccessRulesProtected.ToString() });
                    BottomBarAreAuditRulesCanonical.Inlines.Add(new Run() { Text = fss.AreAuditRulesCanonical.ToString() });
                    BottomBarAreAuditRulesProtected.Inlines.Add(new Run() { Text = fss.AreAuditRulesProtected.ToString() });
                    //BottomBarAuditRulesModified.Text = fss.AuditRulesModified;
                    BottomBarAuditRuleType.Inlines.Add(new Run() { Text = fss.AuditRuleType.Name });
                    //BottomBarGroupModified
                    //BottomBarIsContainer.Text = fss.IsContainer;
                    //BottomBarIsDS.Text = fss.IsDS;
                    //BottomBarOwnerModified.Text = fss.OwnerModified;
                    BottomBarSecurityDescriptor.Inlines.Add(new Run() { Text = fss.GetSecurityDescriptorSddlForm(AccessControlSections.All) });
#endif
                }
            }
        }

        private async void MainListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (mainListView.SelectedItem is IFileSystemEntry item)
            {
                if (item.IsDirectory)
                {
                    DisplayFiles(item.Path);
                }
                else
                {
                    await Launcher.LaunchUriAsync(new(item.Path));
                }
            }
            e.Handled = true;
        }

        private void MainListView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if ((int)e.Key is (> 64 and < 91) or (> 47 and < 58))
            {
                char keyPressed = (char)e.Key;
                if (!keyPressed.Equals(default))
                {
                    int selectIndex = mainListView.SelectedIndex;
                    char charIndex = CurrentFiles[selectIndex].Name[0];
                    if (charIndex == keyPressed)
                    {
                        for (int i = 0; i < CurrentFiles.Count; i++)
                        {
                            if (CurrentFiles[i].Name[0] == keyPressed && i < selectIndex)
                            {
                                mainListView.SelectedIndex = i;
                                return;
                            }
                        }
                    }
                    else
                    {
                        string key = string.Empty + keyPressed;
                        for (int i = 0; i < CurrentFiles.Count; i++)
                        {
                            if (CurrentFiles[i].Name.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (i != selectIndex)// no unnecessary actions
                                {
                                    mainListView.SelectedIndex = i;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                switch (e.Key)
                {
                    case VirtualKey.Cancel:
                        CancelFileTask();
                        return;
                    case VirtualKey.MiddleButton:
                        Debug.WriteLine("Middle button pressed");
                        break;
                    case VirtualKey.XButton1:
                        Debug.WriteLine("XButton1 pressed");
                        break;
                    case VirtualKey.XButton2:
                        Debug.WriteLine("XButton2 pressed");
                        break;
                    case VirtualKey.Enter:
                        if (mainListView.SelectedItem != null)
                        {
                            Debug.WriteLine("Enter pressed, loading: " + CurrentFiles[mainListView.SelectedIndex].Path);
                            DisplayFiles(CurrentFiles[mainListView.SelectedIndex].Path);
                        }
                        return;
                    case VirtualKey.GoBack:
                        Back();
                        return;
                    case VirtualKey.GoForward:
                        Next();
                        return;
                    case VirtualKey.Refresh:
                        Refresh();
                        return;
                    case VirtualKey.Search:
                        // give focus to the autosuggest box
                        break;
                }

            }
        }

        private void Page_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            Debug.WriteLine("Char received : " + args.Character);
            args.Handled = true;
        }

        private void HistoryListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // TODO : 
        }

        private void RefreshFileList_Click(object sender, RoutedEventArgs e) => DisplayFiles(CurrentPath);

        private void CancelButton_Click(object sender, RoutedEventArgs e) => CancelFileTask();

        private void Previous_Click(object sender, RoutedEventArgs e) => Back();

        private void Next_Click(object sender, RoutedEventArgs e) => Next();

        private void PathInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                CompletePath();
            }
        }

        private void PathInput_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string text = args.QueryText;
            if (!string.IsNullOrEmpty(text))
            {
                DisplayFiles(text);
            }
#if TRACE
            else
            {
                Trace.TraceWarning("Submitted path is empty : " + text);
            }
#endif
        }

        private void PathInput_GotFocus(object sender, RoutedEventArgs e) => CompletePath();

        private void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsXButton1Pressed)
            {
                Back();
            }
            else if (e.GetCurrentPoint(null).Properties.IsXButton2Pressed)
            {
                Next();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //App.Settings.Load(this);
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                DisplayFiles(CurrentPath);
            }
        }

        private void OnMainWindowClosed(object sender, WindowEventArgs args)
        {
            CancelFileTask();
            App.MainWindow.Closed -= OnMainWindowClosed;
            // saving when the mainwindow is closed because this page is cached and thus never unloaded
            SavePage();
        }

        private void History_ListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {

        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            var selection = mainListView.SelectedItems;
            bool canObfuscate = false;
            bool canDelete = false;
            bool canCopy = false;
            foreach (var o in selection)
            {
                if (o is IFileSystemEntry entry)
                {
                    if (!canObfuscate && !entry.IsDirectory && !entry.Attributes.HasFlag(FileAttributes.System | FileAttributes.Device))
                    {
                        canObfuscate = true;
                    }

                    if (!entry.IsSystem && !entry.IsDevice)
                    {
                        canCopy = true;
                        canDelete = true;
                    }

                    if (canCopy && canDelete && canObfuscate)
                    {
                        break;
                    }
                }
            }

            deleteFileMenuItem.IsEnabled = canDelete;
            copyFileMenuItem.IsEnabled = canCopy;
            obfuscateFileMenuItem.IsEnabled = canObfuscate;
        }

        private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selection = mainListView.SelectedItems;
            foreach (var o in selection)
            {
                if (o is IFileSystemEntry entry)
                {
                    try
                    {
                        entry.Delete();
                    }
                    catch (IOException ex)
                    {
                        App.TraceError(ex, $"Cannot delete {entry.Name}");
                    } 
                }
            }
        }

        private async void CopyFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selection = mainListView.SelectedItems;

            DataPackage dataPackage = new();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            List<IStorageItem> items = new(selection.Count);
            List<Task<IStorageItem>> tasks = new(selection.Count);
            foreach (var o in selection)
            {
                if (o is IFileSystemEntry entry)
                {
                    try
                    {
                        tasks.Add(entry.AsIStorageItem());
                    }
                    catch (IOException ex)
                    {
                        App.TraceError(ex, $"Cannot copy {entry.Name}");
                    } 
                }
            }

            while (tasks.Count > 0)
            {
                Task<IStorageItem> completed = await Task.WhenAny(tasks);
                if (completed.IsCompletedSuccessfully)
                {
                    items.Add(completed.Result);
                }
                else
                {
                    App.TraceError(completed.Exception, $"Could not copy item to clipboard.");
                }
                tasks.Remove(completed);
            }

            dataPackage.SetStorageItems(items);
            Clipboard.SetContent(dataPackage);
        }

        private async void ObfuscateFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selection = mainListView.SelectedItems;
                List<IFileSystemEntry> entries = new(selection.Count);
                foreach (var o in selection)
                {
                    if (o is IFileSystemEntry entry)
                    {
                        entries.Add(entry);
                    }
                }

                if (entries.Count == 1)
                {
                    FileObfuscator fileObfuscator = new(0);
                    string newPath = await fileObfuscator.Obfuscate(entries[0].Path);
                }
            }
            catch (Exception ex)
            {
                App.TraceError(ex, "Failed to obfuscate files.");
            }
        }
        #endregion

        #region manager
        private void View_SizeChanged(IFileSystemEntry sender, long args)
        {
            if (sortEventStopwatch.ElapsedMilliseconds > 120 && Math.Abs(sender.Size - args) > (sender.Size * 0.2))
            {
                _ = DispatcherQueue.TryEnqueue(() => SortList());
                sortEventStopwatch.Restart();
            }
        }

        private void FileSystemManager_Progress(object sender, string message)
        {
            DisplayMessage(message, false, sender == null);
        }

        private void FileSystemManager_Exception(object sender, Exception exception)
        {
            DisplayMessage(exception.Message, true);
        }

        private void FileSystemManager_Change(object sender, ChangeEventArgs data)
        {
            switch (data.ChangeTypes)
            {
                case ChangeTypes.FileCreated:
                    //AddDelegate(CurrentFiles, data.Entry);
                    break;
                case ChangeTypes.FileDeleted:
                    for (int i = 0; i < CurrentFiles.Count; i++)
                    {
                        if (CurrentFiles[i].FileSystemEntry.Equals(data.Entry))
                        {
                            _ = DispatcherQueue.TryEnqueue(() => CurrentFiles.RemoveAt(i));
                            return;
                        }
                    }
                    break;
                case ChangeTypes.DirectoryCreated:
                    App.TraceWarning("Directory created");
                    break;
                case ChangeTypes.PathDeleted:
                    DispatcherQueue.TryEnqueue(() => CurrentFiles.Clear());
                    App.TraceWarning("Path deleted");
                    break;
            }
        }
        #endregion

        #endregion
    }
}
