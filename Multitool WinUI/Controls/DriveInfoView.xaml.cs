﻿using Microsoft.UI.Xaml.Controls;

using Multitool.DAL;

using MultitoolWinUI.Helpers;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MultitoolWinUI.Controls
{
    public sealed partial class DriveInfoView : UserControl, INotifyPropertyChanged
    {
        private static readonly string[] sysFiles = new string[] { "pagefile.sys", "hiberfil.sys", "swapfile.sys" };
        private readonly Stopwatch stopwatch = new();

        private string _recycleBinSize = string.Empty;
        private double _recycleBinPercentage;
        private double _sysFilesPercentage;
        private long _sysFilesSize;

        public DriveInfoView(DriveInfo driveInfo, CancellationTokenSource cancelToken)
        {
            InitializeComponent();
            DriveInfo = driveInfo;
            SetGradients(false);
            if (DriveInfo.IsReady)
            {
                cancelToken.Token.ThrowIfCancellationRequested();
                _ = LoadComponents(cancelToken);
            }
            else
            {
                Trace.TraceError("Drive could not be loaded. '" + driveInfo.Name + "' was not ready");
                //App.DisplayMessage("Error", "The drive could not be loaded", "Drive name: " + driveInfo.Name);
            }
        }

        #region properties

        public string BackgroudColor { get; set; }

        public DriveInfo DriveInfo { get; }

        public string DriveName => DriveInfo.Name + "(" + DriveInfo?.VolumeLabel + ")";

        public string DriveCapacity => Tool.FormatSize(DriveInfo?.TotalSize ?? 0);

        public string DriveFreeSpace => Tool.FormatSize(DriveInfo?.TotalFreeSpace ?? 0);

        public string SysFilesSize => Tool.FormatSize(_sysFilesSize);

        public double DriveFreeSpacePercentage
        {
            get
            {
                if (DriveInfo != null)
                {
                    return (DriveInfo.TotalFreeSpace / (double)DriveInfo.TotalSize) * 100;
                }
                else
                {
                    return 0;
                }
            }
        }

        public string RecycleBinSize
        {
            get => _recycleBinSize;
            private set
            {
                _recycleBinSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecycleBinSize)));
            }
        }

        public double RecycleBinPercentage
        {
            get => _recycleBinPercentage;
            private set
            {
                _recycleBinPercentage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecycleBinPercentage)));
            }
        }

        public double SysFilesPercentage
        {
            get => _sysFilesPercentage;
            set
            {
                _sysFilesPercentage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SysFilesPercentage)));
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region methods

        private async Task LoadComponents(CancellationTokenSource cancelTokenSource)
        {
            if (DriveInfo == null)
            {
                return;
            }

            CancellationToken cancelToken = cancelTokenSource.Token;
            cancelToken.ThrowIfCancellationRequested();

            long size = await Task.Run(() =>
            {
                return new DirectorySizeCalculator().CalculateDirectorySize(DriveInfo.Name + @"$RECYCLE.BIN\", cancelToken);
            }, cancelToken);

            _ = DispatcherQueue.TryEnqueue(() =>
            {
                RecycleBinSize = Tool.FormatSize(size);
                RecycleBinPercentage = (size / (double)DriveInfo.TotalSize) * 100;
                RecycleBin_TextBlock.Opacity = 1;
            });

            cancelToken.ThrowIfCancellationRequested();

            await GetStaticSysFilesSize(cancelToken);

            _ = DispatcherQueue.TryEnqueue(() => SysFiles_TextBlock.Opacity = 1);

            cancelTokenSource.Dispose();
        }

        private async Task GetStaticSysFilesSize(CancellationToken cancelToken)
        {
            await Task.Run(() =>
            {
                stopwatch.Start();

                for (int i = 0; i < sysFiles.Length; i++)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (File.Exists(DriveInfo.Name + sysFiles[i]))
                    {
                        _sysFilesSize += new FileInfo(DriveInfo.Name + sysFiles[i]).Length;
                    }
                }
                DisplaySysFileSize();
            }, cancelToken);

            cancelToken.ThrowIfCancellationRequested();
            await Task.Run(() => ComputeSysFiles(DriveInfo.Name, cancelToken), cancelToken);
            SetGradients(true);
        }

        private void ComputeSysFiles(string path, CancellationToken cancelToken)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(path);
                for (int i = 0; i < dirs.Length; i++)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    ComputeSysFiles(dirs[i], cancelToken);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }

            try
            {
                string[] files = Directory.GetFiles(path);
                FileInfo fileInfo;
                for (int i = 0; i < files.Length; i++)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (files[i].EndsWith(".sys"))
                    {
                        try
                        {
                            fileInfo = new FileInfo(files[i]);
                            _sysFilesSize += fileInfo.Length;
                            DisplaySysFileSize();
                        }
                        catch (UnauthorizedAccessException) { }
                        catch (FileNotFoundException) { }
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }

        private void DisplaySysFileSize()
        {
            if (stopwatch.ElapsedMilliseconds > 150)
            {
                stopwatch.Reset();

                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SysFilesSize)));
                    SysFilesPercentage = _sysFilesSize / (double)DriveInfo.TotalSize * 100;
                });

                stopwatch.Start();
            }
        }

        private void SetGradients(bool active)
        {
            if (active)
            {
                SetFirstGradient(Tool.GetAppRessource<Color>("SystemAccentColorDark2"));
                SetSecondGradient(Tool.GetAppRessource<Color>("SystemAccentColorDark1"));
                SetThirdGradient(Tool.GetAppRessource<Color>("WarmWhite"));
            }
            else
            {
                SetFirstGradient(Tool.GetAppRessource<Color>("DarkBlack"));
                SetSecondGradient(Tool.GetAppRessource<Color>("LightBlack"));
                SetThirdGradient(Tool.GetAppRessource<Color>("WarmWhite"));
            }
        }

        private void SetFirstGradient(Color value)
        {
            GradientStopOne.Color = value;
        }

        private void SetSecondGradient(Color value)
        {
            GradientStopTwo.Color = value;
        }

        private void SetThirdGradient(Color value)
        {
            GradientStopThree.Color = value;
        }

        #endregion

    }
}