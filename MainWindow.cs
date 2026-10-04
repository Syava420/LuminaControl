using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LuminaControl;

public class MainWindow : Window
{
    private List<IMonitor> _monitors = [];
    private readonly List<MonitorControlGroup> _controlGroups = [];
    
    private StackPanel _monitorContainer = null!;
    private TextBlock _statusText = null!;
    private CheckBox _linkToggle = null!;
    private CheckBox _startupToggle = null!;
    private Button _refreshBtn = null!;
    
    private bool _isUpdatingLinked;
    private readonly Action<bool> _onStartupChanged;

    public bool AllowClose { get; set; }

    public MainWindow(bool startWithWindows, Action<bool> onStartupChanged)
    {
        _onStartupChanged = onStartupChanged;

        // Window Configuration
        Title = "LuminaControl";
        Width = 380;
        Height = 480;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/LuminaControl;component/app.ico", UriKind.RelativeOrAbsolute));
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load window icon: {ex.Message}");
        }

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
                DragMove();
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
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        
        var dotGlow = new DropShadowEffect
        {
            Color = Color.FromRgb(0x63, 0x66, 0xF1),
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.95
        };
        dot.Effect = dotGlow;

        var titleText = new TextBlock
        {
            Text = "LuminaControl",
            FontFamily = Styles.MainFont,
            FontSize = 13,
            Foreground = Styles.BrushText,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        titleStack.Children.Add(dot);
        titleStack.Children.Add(titleText);
        titleBarContent.Children.Add(titleStack);

        // Control Buttons Stack (Minimize, Close)
        var controlStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        
        var minBtn = new Button { Content = "—" };
        Styles.StyleMinButton(minBtn);
        minBtn.Click += (s, e) => Hide();

        var closeBtn = new Button { Content = "✕" };
        Styles.StyleCloseButton(closeBtn);
        closeBtn.Click += (s, e) => Hide(); // Minimize to tray instead of close

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
        _startupToggle.Checked += (s, e) => _onStartupChanged?.Invoke(true);
        _startupToggle.Unchecked += (s, e) => _onStartupChanged?.Invoke(false);

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
        Content = windowFrame;
    }

    private async void RefreshMonitors()
    {
        _refreshBtn.IsEnabled = false;
        _statusText.Text = "Сканирование дисплеев...";
        _monitorContainer.Children.Clear();
        _controlGroups.Clear();

        var discovered = await Task.Run(MonitorManager.DiscoverMonitors);

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
                _statusText.Text = $"Найдено экранов: {_monitors.Count}";
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
        var slider = new ModernSlider { Margin = new Thickness(0, 4, 0, 0) };
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
            valueText.Text = $"{currentBrightness}%";
        }
        else
        {
            Task.Run(() =>
            {
                int currentBrightness = monitor.GetBrightness();
                Dispatcher.Invoke(() =>
                {
                    slider.Value = currentBrightness;
                    valueText.Text = $"{currentBrightness}%";
                });
            });
        }

        // Handle slider updates & linked mode
        slider.ValueChanged += (s, e) =>
        {
            int newValue = (int)slider.Value;
            valueText.Text = $"{newValue}%";

            if (_isUpdatingLinked) return;

            if (_linkToggle.IsChecked == true)
            {
                _isUpdatingLinked = true;
                int delta = newValue - (int)e.OldValue;
                
                foreach (var group in _controlGroups)
                {
                    if (group != controlGroup)
                    {
                        int targetVal = Math.Clamp((int)group.Slider.Value + delta, 0, 100);
                        group.Slider.Value = targetVal;
                        group.UpdateBrightness(targetVal);
                    }
                }
                _isUpdatingLinked = false;
            }

            controlGroup.UpdateBrightness(newValue);
        };
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }
}

internal class MonitorControlGroup
{
    public IMonitor Monitor { get; }
    public ModernSlider Slider { get; }
    public TextBlock ValueText { get; }

    private readonly DispatcherTimer _debounceTimer;
    private int _targetBrightness;
    
    private readonly object _lockObj = new();
    private bool _isWriting;
    private int _pendingBrightness = -1;

    public MonitorControlGroup(IMonitor monitor, ModernSlider slider, TextBlock valueText)
    {
        Monitor = monitor;
        Slider = slider;
        ValueText = valueText;

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;
    }

    public void UpdateBrightness(int value)
    {
        Logger.Log($"MonitorControlGroup({Monitor.Name}): UpdateBrightness called, value = {value}");
        _targetBrightness = value;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        Logger.Log($"MonitorControlGroup({Monitor.Name}): Debounce timer ticked, target = {_targetBrightness}");
        
        lock (_lockObj)
        {
            if (_isWriting)
            {
                Logger.Log($"MonitorControlGroup({Monitor.Name}): Hardware is busy. Saving {_targetBrightness} as pending.");
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
            Logger.Log($"MonitorControlGroup({Monitor.Name}): Writing internal brightness synchronously to {val}...");
            try
            {
                Monitor.SetBrightness(val);
                Logger.Log($"MonitorControlGroup({Monitor.Name}): Synchronous internal write success, val = {val}");
            }
            catch (Exception ex)
            {
                Logger.Log($"MonitorControlGroup({Monitor.Name}): Synchronous internal write FAILED: {ex.Message}");
            }
            finally
            {
                int nextVal = -1;
                lock (_lockObj)
                {
                    if (_pendingBrightness != -1)
                    {
                        nextVal = _pendingBrightness;
                        _pendingBrightness = -1;
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
            Logger.Log($"MonitorControlGroup({Monitor.Name}): Enqueuing background task to write external brightness to {val}...");
            Task.Run(() =>
            {
                try
                {
                    Monitor.SetBrightness(val);
                    Logger.Log($"MonitorControlGroup({Monitor.Name}): Background write success, val = {val}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"MonitorControlGroup({Monitor.Name}): Background write FAILED: {ex.Message}");
                }
                finally
                {
                    int nextVal = -1;
                    lock (_lockObj)
                    {
                        if (_pendingBrightness != -1)
                        {
                            nextVal = _pendingBrightness;
                            _pendingBrightness = -1;
                        }
                        else
                        {
                            _isWriting = false;
                        }
                    }

                    if (nextVal != -1)
                    {
                        Application.Current.Dispatcher.Invoke(() => StartWriteTask(nextVal));
                    }
                }
            });
        }
    }
}
