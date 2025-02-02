﻿using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MultitoolWinUI.Controls
{
    public sealed partial class AppMessageControl : UserControl
    {
        private readonly ConcurrentQueue<DispatcherQueueHandler> displayQueue = new();
        private readonly Timer messageTimer = new() { AutoReset = true, Enabled = false, Interval = 3500 };
        private readonly object _lock = new();
        private volatile bool busy;
        private bool closed;
        private bool hasFocus;

        public AppMessageControl()
        {
            InitializeComponent();
            if (DispatcherQueue != null)
            {
                DispatcherQueue.ShutdownStarting += DispatcherQueue_ShutdownStarting;
            }
            messageTimer.Elapsed += MessageTimer_Elapsed;
            closed = false;
        }

        public event TypedEventHandler<AppMessageControl, Visibility> VisibilityChanged;

        #region properties

        #region dependency properties
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(AppMessageControl), new(string.Empty));

        public static readonly DependencyProperty TitleGlyphProperty = DependencyProperty.Register(nameof(TitleGlyph), typeof(string), typeof(AppMessageControl), new("\xE783"));

        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(MessageProperty), typeof(object), typeof(AppMessageControl), new(null));
        #endregion

        /// <summary>
        /// Glyph to append to the title.
        /// </summary>
        public string TitleGlyph
        {
            get => (string)GetValue(TitleGlyphProperty);
            set => SetValue(TitleGlyphProperty, value);
        }

        /// <summary>
        /// Header of the optional control's content.
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public bool Sync { get; set; } = true;
        #endregion

        public void QueueMessage(string title, string message, Brush background)
        {
            if (DispatcherQueue != null)
            {
                lock (_lock)
                {
                    if (!busy)
                    {
                        if (!closed)
                        {
                            busy = true;
                            _ = DispatcherQueue.TryEnqueue(() => DisplayMessage(title, message, background));
                        }
                    }
                    else
                    {
                        displayQueue.Enqueue(() => DisplayMessage(title, message, background));
                    }
                }
            }
        }

        public void Silence()
        {
            closed = true;
            messageTimer.Stop();
            displayQueue.Clear();
        }

        #region private methods
        private void Dump()
        {
            StringBuilder builder = new();
            _ = builder.AppendLine(nameof(AppMessageControl) + " trace stack dump:");
            _ = builder.Append("\tQueued callbacks ");
            _ = builder.Append(displayQueue.Count);
            displayQueue.Clear();
            Trace.WriteLine(builder.ToString());
        }

        private void DisplayMessage(string title, string message, Brush background)
        {
            if (!closed)
            {
                ContentGrid.Background = background;
                Title = title;
                Message = message;
                messageTimer.Start();
                VisibilityChanged?.Invoke(this, Visibility.Visible);
            }
        }

        private bool CheckForCallbacks()
        {
            if (!displayQueue.IsEmpty)
            {
                if (DispatcherQueue != null && displayQueue.TryDequeue(out DispatcherQueueHandler next))
                {
                    DispatcherQueue.TryEnqueue(next);
                }
#if TRACE
                else
                {
                    Trace.TraceWarning("Unable to dequeue action from display queue");
                }
#endif
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region event handlers
        private void DispatcherQueue_ShutdownStarting(DispatcherQueue sender, DispatcherQueueShutdownStartingEventArgs args)
        {
            closed = true;
            busy = false;
            messageTimer.Stop();
            _ = Task.Run(Dump);
            Trace.WriteLine("Dispatcher shutting down");
        }

        private void MessageTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
#if true
            if (!hasFocus && !CheckForCallbacks())
            {
                // no messages, close + stop timer
                busy = false;
                messageTimer.Stop();
                if (Sync)
                {
                    DispatcherQueue.TryEnqueue(() => VisibilityChanged?.Invoke(this, Visibility.Collapsed));
                }
                else
                {
                    VisibilityChanged?.Invoke(this, Visibility.Collapsed);
                }
            } 
#endif
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckForCallbacks())
            {
                busy = false;
                VisibilityChanged?.Invoke(this, Visibility.Collapsed);
            }
        }

        private void Control_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            hasFocus = true;
        }

        private void Control_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            hasFocus = false;
        }

        private void Control_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            hasFocus = true;
            messageTimer.Stop();
            //messageTimer.
        }

        private void Control_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            hasFocus = false;
            messageTimer.Start();
        }
        #endregion
    }
}
