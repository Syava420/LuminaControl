using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace LuminaControl;

public static class Program
{
    private static Mutex? _instanceMutex;
    private static System.Windows.Forms.NotifyIcon? _notifyIcon;
    private static MainWindow? _mainWindow;
    private static Icon? _trayIcon;

    [STAThread]
    public static void Main()
    {
        Logger.Init();
        Logger.Log("App starting on modern .NET 8...");
        
        // Initialize WinForms visual styles for ContextMenuStrip stability
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);

        try
        {
            // 1. Single Instance Check using Mutex & Restore Event
            const string restoreEventName = "LuminaControl_RestoreWindow_Event";
            _instanceMutex = new Mutex(true, "LuminaControl_SingleInstance_Mutex_Key", out var isNewInstance);

            if (!isNewInstance)
            {
                // Signal running instance to show/restore its window
                try
                {
                    using var evt = EventWaitHandle.OpenExisting(restoreEventName);
                    evt.Set();
                }
                catch { }
                return;
            }

            // Listen for restore events from subsequent launches
            var restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, restoreEventName);
            ThreadPool.RegisterWaitForSingleObject(restoreEvent, (state, timedOut) =>
            {
                Application.Current?.Dispatcher?.Invoke(() => RestoreWindow());
            }, null, -1, false);

            // 2. Initialize WPF Application
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Global Exception Handlers
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Logger.Log($"GLOBAL UNHANDLED EXCEPTION: {e.ExceptionObject}");
            };
            app.DispatcherUnhandledException += (s, e) =>
            {
                Logger.Log($"DISPATCHER UNHANDLED EXCEPTION: {e.Exception}");
                e.Handled = true;
            };
            System.Windows.Forms.Application.ThreadException += (s, e) =>
            {
                Logger.Log($"WINFORMS THREAD EXCEPTION: {e.Exception}");
            };

            // 3. Get Current Startup Settings and Check if Minimized
            bool startWithWindows = GetStartupSetting();
            bool startMinimized = false;
            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                    break;
                }
            }

            // 4. Create Main Window
            _mainWindow = new MainWindow(startWithWindows, SetStartupSetting);
            if (!startMinimized)
            {
                _mainWindow.Show();
            }

            // 5. Setup System Tray Icon
            SetupTrayIcon(app);

            // 6. Run Application Event Loop
            app.Run();

            // 7. Cleanup on Exit
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _trayIcon?.Dispose();
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LuminaControl_Error.txt"), ex.ToString());
            }
            catch { }
            MessageBox.Show($"Критическая ошибка при запуске:\n{ex}", "LuminaControl Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SetupTrayIcon(Application app)
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon();
        _notifyIcon.Text = "LuminaControl - Яркость мониторов";
        _trayIcon = GetTrayIcon();
        _notifyIcon.Icon = _trayIcon;
        _notifyIcon.Visible = true;

        // Double click restores the window
        _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
        
        // Left click restores the window as well (convenient for user)
        _notifyIcon.Click += (s, e) =>
        {
            if (e is System.Windows.Forms.MouseEventArgs mouseArgs && mouseArgs.Button == System.Windows.Forms.MouseButtons.Left)
            {
                RestoreWindow();
            }
        };

        // Context Menu with Dark Theme
        var contextMenu = new System.Windows.Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            BackColor = System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C),
            ForeColor = System.Drawing.Color.FromArgb(0xF3, 0xF4, 0xF6),
            Font = new System.Drawing.Font("Segoe UI", 9f),
            Renderer = new DarkMenuRenderer()
        };
        
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Открыть LuminaControl");
        openItem.Click += (s, e) => RestoreWindow();
        openItem.ForeColor = System.Drawing.Color.FromArgb(0xF3, 0xF4, 0xF6);
        
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Выход");
        exitItem.Click += (s, e) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon?.Dispose();
            if (_mainWindow != null)
            {
                _mainWindow.AllowClose = true;
            }
            app.Shutdown();
        };
        exitItem.ForeColor = System.Drawing.Color.FromArgb(0xF3, 0xF4, 0xF6);

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator()); // Separator
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private static Icon GetTrayIcon()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
            {
                var assoc = Icon.ExtractAssociatedIcon(exePath);
                if (assoc != null) return assoc;
            }
        }
        catch { }

        try
        {
            string localIco = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(localIco))
            {
                return new Icon(localIco);
            }
        }
        catch { }

        return GenerateTrayIcon();
    }

    private static Icon GenerateTrayIcon()
    {
        try
        {
            // Fallback: Generate a crisp white minimalist sun icon in memory
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Crisp light off-white color (clearly visible on dark taskbars)
                using var pen = new Pen(Color.FromArgb(0xF3, 0xF4, 0xF6), 2.5f);
                
                // Draw sun center
                g.DrawEllipse(pen, 10, 10, 12, 12);

                // Draw rays
                int cx = 16, cy = 16;
                double r1 = 9.0;
                double r2 = 13.0;
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    float x1 = (float)(cx + r1 * Math.Cos(angle));
                    float y1 = (float)(cy + r1 * Math.Sin(angle));
                    float x2 = (float)(cx + r2 * Math.Cos(angle));
                    float y2 = (float)(cy + r2 * Math.Sin(angle));
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
            
            // Create valid icon handle
            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to generate tray icon: {ex.Message}");
            return SystemIcons.Application; // Fallback
        }
    }

    private static void RestoreWindow()
    {
        if (_mainWindow == null) return;

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    // Startup Registry Helpers
    private static bool GetStartupSetting()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (key != null)
            {
                return key.GetValue("LuminaControl") != null;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error reading startup registry key: {ex.Message}");
        }
        return false;
    }

    private static void SetStartupSetting(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enable)
                {
                    string path = Assembly.GetExecutingAssembly().Location;
                    // In modern .NET Core, Assembly.GetExecutingAssembly().Location might return dll path, 
                    // so we use AppContext.BaseDirectory or Process path instead!
                    string exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory + "LuminaControl.exe";
                    key.SetValue("LuminaControl", $"\"{exePath}\" --minimized");
                    Logger.Log($"Auto-startup enabled for path: {exePath} --minimized");
                }
                else
                {
                    key.DeleteValue("LuminaControl", false);
                    Logger.Log("Auto-startup disabled");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error setting startup registry key: {ex.Message}");
        }
    }
}

internal class DarkColorTable : System.Windows.Forms.ProfessionalColorTable
{
    public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C);
    public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C);
    public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C);
    public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C);
    public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(0x2A, 0x2F, 0x35);
    public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.Transparent;
    public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(0x2A, 0x2F, 0x35);
    public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(0x2A, 0x2F, 0x35);
    public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(0x2A, 0x2F, 0x35);
    public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C);
    public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(0x16, 0x18, 0x1C);
    public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(0x2A, 0x2F, 0x35);
    public override System.Drawing.Color SeparatorLight => System.Drawing.Color.Transparent;
}

internal class DarkMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = System.Drawing.Color.FromArgb(0xF3, 0xF4, 0xF6); // Always light text
        base.OnRenderItemText(e);
    }
    
    protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0x2A, 0x2F, 0x35), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
}
