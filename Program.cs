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
            // 1. Single Instance Check using Mutex
            _instanceMutex = new Mutex(true, "LuminaControl_SingleInstance_Mutex_Key", out var isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show(
                    "LuminaControl уже запущен. Проверьте системный трей.",
                    "LuminaControl",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

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
                System.IO.File.WriteAllText(@"C:\Users\Neuron\Desktop\LuminaControl_Error.txt", ex.ToString());
            }
            catch { }
            MessageBox.Show($"Критическая ошибка при запуске:\n{ex}", "LuminaControl Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SetupTrayIcon(Application app)
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon();
        _notifyIcon.Text = "LuminaControl - Яркость мониторов";
        _trayIcon = GenerateTrayIcon();
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

        // Context Menu
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Открыть LuminaControl");
        openItem.Click += (s, e) => RestoreWindow();
        
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

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator()); // Separator
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private static Icon GenerateTrayIcon()
    {
        try
        {
            // Generate a custom icon in memory (monochrome dark slate sun)
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Dark slate charcoal color (crisp line)
                using var pen = new Pen(Color.FromArgb(0x3E, 0x44, 0x4D), 2.5f);
                
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
