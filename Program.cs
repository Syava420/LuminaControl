using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace LuminaControl
{
    public static class Program
    {
        private static Mutex _instanceMutex;
        private static System.Windows.Forms.NotifyIcon _notifyIcon;
        private static MainWindow _mainWindow;
        private static Icon _trayIcon;

        [STAThread]
        public static void Main()
        {
            Logger.Init();
            Logger.Log("App starting...");
            try
            {
                // 1. Single Instance Check using Mutex
                bool isNewInstance;
                _instanceMutex = new Mutex(true, "LuminaControl_SingleInstance_Mutex_Key", out isNewInstance);

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

                // 3. Get Current Startup Settings
                bool startWithWindows = GetStartupSetting();

                // 4. Create and Show Main Window
                _mainWindow = new MainWindow(startWithWindows, SetStartupSetting);
                _mainWindow.Show();

                // 5. Initialize System Tray Icon
                SetupTrayIcon(app);

                // 6. Run the WPF Event Loop
                app.Run();

                // Cleanup Mutex on exit
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.File.WriteAllText(@"C:\Users\Neuron\Desktop\LuminaControl_Error.txt", ex.ToString());
                }
                catch {}
                MessageBox.Show("Критическая ошибка при запуске:\n" + ex.ToString(), "LuminaControl Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var mouseArgs = e as System.Windows.Forms.MouseEventArgs;
                if (mouseArgs != null && mouseArgs.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    RestoreWindow();
                }
            };

            // Context Menu
            var contextMenu = new System.Windows.Forms.ContextMenu();
            
            var openItem = new System.Windows.Forms.MenuItem("Открыть LuminaControl");
            openItem.Click += (s, e) => RestoreWindow();
            
            var exitItem = new System.Windows.Forms.MenuItem("Выход");
            exitItem.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                if (_trayIcon != null)
                {
                    _trayIcon.Dispose();
                }
                app.Shutdown();
            };

            contextMenu.MenuItems.Add(openItem);
            contextMenu.MenuItems.Add(new System.Windows.Forms.MenuItem("-")); // Separator
            contextMenu.MenuItems.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;
        }

        private static void RestoreWindow()
        {
            if (_mainWindow != null)
            {
                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.Show();
                }
                
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                }
                
                _mainWindow.Activate();
                _mainWindow.Focus();
            }
        }

        private static Icon GenerateTrayIcon()
        {
            try
            {
                // Generate a custom icon in memory (Indigo circle with a white glowing center)
                using (var bmp = new Bitmap(32, 32))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);

                        // Outer glowing blue/indigo ring
                        using (var brushOuter = new SolidBrush(Color.FromArgb(0x63, 0x66, 0xF1)))
                        {
                            g.FillEllipse(brushOuter, 4, 4, 24, 24);
                        }

                        // Inner glowing white core
                        using (var brushInner = new SolidBrush(Color.White))
                        {
                            g.FillEllipse(brushInner, 10, 10, 12, 12);
                        }
                    }
                    IntPtr hIcon = bmp.GetHicon();
                    return Icon.FromHandle(hIcon);
                }
            }
            catch
            {
                // Fallback to standard system icon if drawing fails
                return SystemIcons.Application;
            }
        }

        private static bool GetStartupSetting()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        return key.GetValue("LuminaControl") != null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading registry: " + ex.Message);
            }
            return false;
        }

        private static void SetStartupSetting(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string appPath = Assembly.GetExecutingAssembly().Location;
                            // Make sure the path is quoted to handle spaces properly
                            key.SetValue("LuminaControl", "\"" + appPath + "\"");
                        }
                        else
                        {
                            key.DeleteValue("LuminaControl", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось изменить параметры автозапуска: " + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
