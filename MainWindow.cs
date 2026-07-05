using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LuminaControl
{
    public class MainWindow : Window
    {
        private List<IMonitor> _monitors = new List<IMonitor>();
        private List<MonitorControlGroup> _controlGroups = new List<MonitorControlGroup>();
        
        private StackPanel _monitorContainer;
        private TextBlock _statusText;
        private CheckBox _linkToggle;
        private CheckBox _startupToggle;
        private Button _refreshBtn;
        
        private bool _isUpdatingLinked = false;
        private Action<bool> _onStartupChanged;

        public MainWindow(bool startWithWindows, Action<bool> onStartupChanged)
        {
            _onStartupChanged = onStartupChanged;

            // Window Configuration
            this.Title = "LuminaControl";
            this.Width = 380;
            this.Height = 480;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Create Visual Hierarchy
            InitializeUI(startWithWindows);
            
            // Initial Monitor Discovery
            RefreshMonitors();
        }

        private void InitializeUI(bool startWithWindows)
        {
            // Outer Border (Window Frame with Shadow)
            var windowFrame = new Border
            {
                Background = Styles.BrushBg,
                BorderBrush = Styles.BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(0),
                Margin = new Thickness(10) // Margin allows the drop shadow to be visible
            };

            // Drop Shadow
            var shadow = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 15,
                ShadowDepth = 2,
                Opacity = 0.55
            };
            windowFrame.Effect = shadow;

            // Main Grid
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // Title Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) }); // Bottom Controls

            // 1. TITLE BAR
            var titleBar = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x1C)),
                SnapsToDevicePixels = true
            };
            
            // Rounded corners on top for title bar matching window frame (12 radius - 1 border)
            var titleBarClip = new Border
            {
                Background = titleBar.Background,
                CornerRadius = new CornerRadius(11, 11, 0, 0)
            };
            
            // Drag support
            titleBarClip.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
            };

            var titleBarContent = new Grid();
            titleBarContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBarContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title Text & Glow Indicator
            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Styles.BrushAccent,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var titleText = new TextBlock
            {
                Text = "LuminaControl",
                FontFamily = Styles.MainFont,
                FontSize = 14,
                Foreground = Styles.BrushText,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            
            titleStack.Children.Add(dot);
            titleStack.Children.Add(titleText);
            titleBarContent.Children.Add(titleStack);

            // Window controls (Min, Close)
            var controlStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            
            var minBtn = new Button
            {
                Content = "—"
            };
            Styles.StyleMinButton(minBtn);
            minBtn.Click += (s, e) => this.WindowState = WindowState.Minimized;
            
            var closeBtn = new Button
            {
                Content = "✕"
            };
            Styles.StyleCloseButton(closeBtn);
            closeBtn.Click += (s, e) => this.Hide(); // Minimize to tray instead of close

            controlStack.Children.Add(minBtn);
            controlStack.Children.Add(closeBtn);
            Grid.SetColumn(controlStack, 1);
            titleBarContent.Children.Add(controlStack);

            titleBarClip.Child = titleBarContent;
            mainGrid.Children.Add(titleBarClip);

            // 2. MAIN CONTENT (Scrollable Monitor Cards)
            var contentGrid = new Grid { Margin = new Thickness(16, 12, 16, 12) };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Monitor list

            // Header labels
            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var headerTitle = new TextBlock
            {
                Text = "Яркость экранов",
                FontFamily = Styles.MainFont,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Styles.BrushText
            };
            _statusText = new TextBlock
            {
                Text = "Поиск подключенных мониторов...",
                FontFamily = Styles.MainFont,
                FontSize = 11,
                Foreground = Styles.BrushTextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            headerStack.Children.Add(headerTitle);
            headerStack.Children.Add(_statusText);
            contentGrid.Children.Add(headerStack);

            // ScrollViewer for monitor list
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _monitorContainer = new StackPanel();
            scrollViewer.Content = _monitorContainer;
            
            Grid.SetRow(scrollViewer, 1);
            contentGrid.Children.Add(scrollViewer);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // 3. BOTTOM PANEL (Options & Refresh)
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x1C)),
                CornerRadius = new CornerRadius(0, 0, 11, 11),
                Padding = new Thickness(16, 0, 16, 0),
                BorderBrush = Styles.BrushBorder,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            var bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Toggles stack
            var togglesStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            _linkToggle = new CheckBox
            {
                Content = "Связать",
                FontFamily = Styles.MainFont,
                FontSize = 11,
                Foreground = Styles.BrushTextMuted,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Styles.StyleCheckBoxAsSwitch(_linkToggle);

            _startupToggle = new CheckBox
            {
                Content = "Автозапуск",
                FontFamily = Styles.MainFont,
                FontSize = 11,
                Foreground = Styles.BrushTextMuted,
                IsChecked = startWithWindows,
                VerticalAlignment = VerticalAlignment.Center
            };
            Styles.StyleCheckBoxAsSwitch(_startupToggle);
            _startupToggle.Checked += (s, e) => { if (_onStartupChanged != null) _onStartupChanged(true); };
            _startupToggle.Unchecked += (s, e) => { if (_onStartupChanged != null) _onStartupChanged(false); };

            togglesStack.Children.Add(_linkToggle);
            togglesStack.Children.Add(_startupToggle);
            bottomGrid.Children.Add(togglesStack);

            // Refresh Button
            _refreshBtn = new Button
            {
                Content = "Обновить",
                VerticalAlignment = VerticalAlignment.Center
            };
            Styles.StyleActionButton(_refreshBtn);
            _refreshBtn.Click += (s, e) => RefreshMonitors();
            
            Grid.SetColumn(_refreshBtn, 1);
            bottomGrid.Children.Add(_refreshBtn);

            bottomBar.Child = bottomGrid;
            Grid.SetRow(bottomBar, 2);
            mainGrid.Children.Add(bottomBar);

            windowFrame.Child = mainGrid;
            this.Content = windowFrame;
        }

        private async void RefreshMonitors()
        {
            _refreshBtn.IsEnabled = false;
            _statusText.Text = "Сканирование дисплеев...";
            _monitorContainer.Children.Clear();
            _controlGroups.Clear();

            // Run monitor discovery on a background thread to prevent UI freezing
            var discovered = await Task.Run(() => MonitorManager.DiscoverMonitors());

            Dispatcher.Invoke(() =>
            {
                _monitors = discovered;
                if (_monitors.Count == 0)
                {
                    _statusText.Text = "Мониторы не найдены.";
                    var noMonitorsLbl = new TextBlock
                    {
                        Text = "Не найдено мониторов с поддержкой DDC/CI или WMI.",
                        Foreground = Styles.BrushTextMuted,
                        FontFamily = Styles.MainFont,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 40, 0, 0),
                        TextAlignment = TextAlignment.Center
                    };
                    _monitorContainer.Children.Add(noMonitorsLbl);
                }
                else
                {
                    _statusText.Text = string.Format("Найдено экранов: {0}", _monitors.Count);
                    foreach (var monitor in _monitors)
                    {
                        CreateMonitorWidget(monitor);
                    }
                }

                _refreshBtn.IsEnabled = true;
            });
        }

        private void CreateMonitorWidget(IMonitor monitor)
        {
            var card = new Border
            {
                Background = Styles.BrushCard,
                BorderBrush = Styles.BrushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 8, 0, 8)
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Monitor Header (Name & Value)
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = monitor.Name,
                FontFamily = Styles.MainFont,
                FontSize = 13,
                Foreground = Styles.BrushText,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            headerGrid.Children.Add(nameText);

            var valueText = new TextBlock
            {
                Text = "--%",
                FontFamily = Styles.MainFont,
                FontSize = 13,
                Foreground = Styles.BrushAccent,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(valueText, 1);
            headerGrid.Children.Add(valueText);

            grid.Children.Add(headerGrid);

            // Slider
            var slider = new ModernSlider
            {
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(slider, 1);
            grid.Children.Add(slider);

            card.Child = grid;
            _monitorContainer.Children.Add(card);

            // Initialize control group logic
            var controlGroup = new MonitorControlGroup(monitor, slider, valueText);
            _controlGroups.Add(controlGroup);

            // Set current value
            if (monitor.IsInternal)
            {
                int currentBrightness = monitor.GetBrightness();
                slider.Value = currentBrightness;
                valueText.Text = string.Format("{0}%", currentBrightness);
            }
            else
            {
                Task.Run(() =>
                {
                    int currentBrightness = monitor.GetBrightness();
                    Dispatcher.Invoke(() =>
                    {
                        slider.Value = currentBrightness;
                        valueText.Text = string.Format("{0}%", currentBrightness);
                    });
                });
            }

            // Handle slider updates & linked mode
            slider.ValueChanged += (s, e) =>
            {
                int newValue = (int)slider.Value;
                valueText.Text = string.Format("{0}%", newValue);

                // If updating linked monitors, do not trigger cascade
                if (_isUpdatingLinked) return;

                if (_linkToggle.IsChecked == true)
                {
                    _isUpdatingLinked = true;
                    int delta = newValue - (int)e.OldValue;
                    
                    foreach (var group in _controlGroups)
                    {
                        if (group != controlGroup)
                        {
                            int targetVal = Math.Min(100, Math.Max(0, (int)group.Slider.Value + delta));
                            group.Slider.Value = targetVal;
                            group.UpdateBrightness(targetVal);
                        }
                    }
                    _isUpdatingLinked = false;
                }

                controlGroup.UpdateBrightness(newValue);
            };
        }
    }

    internal class MonitorControlGroup
    {
        public IMonitor Monitor { get; private set; }
        public ModernSlider Slider { get; private set; }
        public TextBlock ValueText { get; private set; }

        private DispatcherTimer _debounceTimer;
        private int _targetBrightness;
        
        private readonly object _lockObj = new object();
        private bool _isWriting = false;
        private int _pendingBrightness = -1;

        public MonitorControlGroup(IMonitor monitor, ModernSlider slider, TextBlock valueText)
        {
            Monitor = monitor;
            Slider = slider;
            ValueText = valueText;

            // Set up dispatcher timer for DDC/CI debouncing (50ms interval)
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public void UpdateBrightness(int value)
        {
            Logger.Log(string.Format("MonitorControlGroup({0}): UpdateBrightness called, value = {1}", Monitor.Name, value));
            _targetBrightness = value;
            // Reset the timer on slide. It will tick 50ms after the user stops moving the slider or slows down.
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            Logger.Log(string.Format("MonitorControlGroup({0}): Debounce timer ticked, target = {1}", Monitor.Name, _targetBrightness));
            
            lock (_lockObj)
            {
                if (_isWriting)
                {
                    Logger.Log(string.Format("MonitorControlGroup({0}): Hardware is busy. Saving {1} as pending.", Monitor.Name, _targetBrightness));
                    // If already writing, save this as the next target and return
                    _pendingBrightness = _targetBrightness;
                    return;
                }
                _isWriting = true;
            }

            StartWriteTask(_targetBrightness);
        }

        private void StartWriteTask(int val)
        {
            if (Monitor.IsInternal)
            {
                Logger.Log(string.Format("MonitorControlGroup({0}): Writing internal brightness synchronously to {1}...", Monitor.Name, val));
                // Internal monitor (WMI) - run on UI thread to prevent COM deadlock
                try
                {
                    Monitor.SetBrightness(val);
                    Logger.Log(string.Format("MonitorControlGroup({0}): Synchronous internal write success, val = {1}", Monitor.Name, val));
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("MonitorControlGroup({0}): Synchronous internal write FAILED: {1}", Monitor.Name, ex.Message));
                }
                finally
                {
                    int nextVal = -1;
                    lock (_lockObj)
                    {
                        if (_pendingBrightness != -1)
                        {
                            nextVal = _pendingBrightness;
                            _pendingBrightness = -1; // Reset pending
                        }
                        else
                        {
                            _isWriting = false;
                        }
                    }

                    if (nextVal != -1)
                    {
                        StartWriteTask(nextVal);
                    }
                }
            }
            else
            {
                Logger.Log(string.Format("MonitorControlGroup({0}): Enqueuing background task to write external brightness to {1}...", Monitor.Name, val));
                // External monitor (DDC/CI) - run on background thread
                Task.Run(() =>
                {
                    try
                    {
                        Monitor.SetBrightness(val);
                        Logger.Log(string.Format("MonitorControlGroup({0}): Background write success, val = {1}", Monitor.Name, val));
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(string.Format("MonitorControlGroup({0}): Background write FAILED: {1}", Monitor.Name, ex.Message));
                    }
                    finally
                    {
                        int nextVal = -1;
                        lock (_lockObj)
                        {
                            if (_pendingBrightness != -1)
                            {
                                nextVal = _pendingBrightness;
                                _pendingBrightness = -1; // Reset pending
                            }
                            else
                            {
                                _isWriting = false;
                            }
                        }

                        if (nextVal != -1)
                        {
                            // Trigger next write by invoking back to UI thread safely
                            Application.Current.Dispatcher.Invoke(new Action(() => StartWriteTask(nextVal)));
                        }
                    }
                });
            }
        }
    }
}
