using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace LuminaControl
{
    public interface IMonitor
    {
        string Id { get; }
        string Name { get; }
        bool IsInternal { get; }
        int GetBrightness();
        void SetBrightness(int brightness);
    }

    public class InternalMonitor : IMonitor
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public bool IsInternal { get { return true; } }

        public InternalMonitor(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public int GetBrightness()
        {
            Logger.Log("InternalMonitor: GetBrightness starting WMI query...");
            try
            {
                using (var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery("WmiMonitorBrightness")))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mObj in collection)
                        {
                            // Match instance name if possible, or just return first
                            object instNameObj = mObj["InstanceName"];
                            string instanceName = instNameObj != null ? instNameObj.ToString() : null;
                            if (string.IsNullOrEmpty(Id) || instanceName == Id)
                            {
                                int curBrightness = Convert.ToInt32(mObj["CurrentBrightness"]);
                                Logger.Log("InternalMonitor: GetBrightness success, val = " + curBrightness);
                                return curBrightness;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading WMI brightness: " + ex.Message);
            }
            return 50; // Fallback
        }

        public void SetBrightness(int brightness)
        {
            if (brightness < 0) brightness = 0;
            Logger.Log("InternalMonitor: SetBrightness to " + brightness + " starting WMI query...");
            try
            {
                using (var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery("WmiMonitorBrightnessMethods")))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mObj in collection)
                        {
                            object instNameObj = mObj["InstanceName"];
                            string instanceName = instNameObj != null ? instNameObj.ToString() : null;
                            if (string.IsNullOrEmpty(Id) || instanceName == Id)
                            {
                                Logger.Log("InternalMonitor: WmiSetBrightness WMI method invoking for value " + brightness + "...");
                                mObj.InvokeMethod("WmiSetBrightness", new object[] { uint.MaxValue, (byte)brightness });
                                Logger.Log("InternalMonitor: WmiSetBrightness WMI method returned");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error setting WMI brightness: " + ex.Message);
            }
        }
    }

    public class ExternalMonitor : IMonitor
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public bool IsInternal { get { return false; } }
        private int _index; // The global index of this physical monitor

        public ExternalMonitor(string id, string name, int index)
        {
            Id = id;
            Name = name;
            _index = index;
        }

        public int GetBrightness()
        {
            Logger.Log("ExternalMonitor(" + Name + "): GetBrightness start");
            int currentBrightness = 50;
            MonitorApiHelper.OperateOnPhysicalMonitor(_index, (hPhys) =>
            {
                uint min, cur, max;
                Logger.Log("ExternalMonitor(" + Name + "): calling GetMonitorBrightness API...");
                if (MonitorApiHelper.GetMonitorBrightness(hPhys, out min, out cur, out max))
                {
                    currentBrightness = (int)cur;
                    Logger.Log("ExternalMonitor(" + Name + "): GetMonitorBrightness API success, val = " + cur);
                }
                else
                {
                    Logger.Log("ExternalMonitor(" + Name + "): GetMonitorBrightness API FAILED");
                }
            });
            return currentBrightness;
        }

        public void SetBrightness(int brightness)
        {
            if (brightness < 0) brightness = 0;
            if (brightness > 100) brightness = 100;

            Logger.Log("ExternalMonitor(" + Name + "): SetBrightness to " + brightness + " start");
            MonitorApiHelper.OperateOnPhysicalMonitor(_index, (hPhys) =>
            {
                Logger.Log("ExternalMonitor(" + Name + "): calling SetMonitorBrightness API for value " + brightness + "...");
                bool success = MonitorApiHelper.SetMonitorBrightness(hPhys, (uint)brightness);
                Logger.Log("ExternalMonitor(" + Name + "): SetMonitorBrightness API returned, success = " + success);
            });
        }
    }

    internal static class MonitorApiHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        public delegate void PhysicalMonitorAction(IntPtr hPhysicalMonitor);

        public static void OperateOnPhysicalMonitor(int targetIndex, PhysicalMonitorAction action)
        {
            int currentIndex = 0;
            bool found = false;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                if (found) return true;

                uint numPhysicalMonitors = 0;
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out numPhysicalMonitors) && numPhysicalMonitors > 0)
                {
                    PHYSICAL_MONITOR[] physicalMonitors = new PHYSICAL_MONITOR[numPhysicalMonitors];
                    if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numPhysicalMonitors, physicalMonitors))
                    {
                        for (int i = 0; i < numPhysicalMonitors; i++)
                        {
                            if (currentIndex == targetIndex)
                            {
                                Logger.Log("MonitorApiHelper: invoking delegate action for physical monitor handle " + physicalMonitors[i].hPhysicalMonitor + "...");
                                action(physicalMonitors[i].hPhysicalMonitor);
                                Logger.Log("MonitorApiHelper: delegate action returned");
                                found = true;
                            }
                            currentIndex++;
                        }
                        DestroyPhysicalMonitors(numPhysicalMonitors, physicalMonitors);
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
    }

    public static class MonitorManager
    {
        public static List<IMonitor> DiscoverMonitors()
        {
            var monitors = new List<IMonitor>();

            // 1. Discover internal laptop monitors via WMI
            try
            {
                using (var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery("WmiMonitorBrightness")))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mObj in collection)
                        {
                            object instNameObj = mObj["InstanceName"];
                            string instanceName = instNameObj != null ? instNameObj.ToString() : null;
                            string friendlyName = "Встроенный экран";
                            monitors.Add(new InternalMonitor(instanceName, friendlyName));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("WMI not supported or failed: " + ex.Message);
            }

            // 2. Discover external monitors via DDC/CI (Physical Monitor APIs)
            int physicalIndex = 0;
            MonitorApiHelper.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref MonitorApiHelper.RECT lprcMonitor, IntPtr dwData)
            {
                uint numPhysicalMonitors = 0;
                if (MonitorApiHelper.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out numPhysicalMonitors) && numPhysicalMonitors > 0)
                {
                    var physicalMonitors = new MonitorApiHelper.PHYSICAL_MONITOR[numPhysicalMonitors];
                    if (MonitorApiHelper.GetPhysicalMonitorsFromHMONITOR(hMonitor, numPhysicalMonitors, physicalMonitors))
                    {
                        for (int i = 0; i < numPhysicalMonitors; i++)
                        {
                            IntPtr hPhys = physicalMonitors[i].hPhysicalMonitor;
                            string desc = physicalMonitors[i].szPhysicalMonitorDescription;
                            
                            // Verify DDC/CI support by attempting to get brightness
                            uint min, cur, max;
                            bool ddcSupported = MonitorApiHelper.GetMonitorBrightness(hPhys, out min, out cur, out max);

                            if (ddcSupported)
                            {
                                string monitorId = "EXTERNAL_" + physicalIndex;
                                string displayName = string.IsNullOrEmpty(desc) ? "Внешний монитор " + (physicalIndex + 1) : desc;
                                monitors.Add(new ExternalMonitor(monitorId, displayName, physicalIndex));
                            }
                            
                            physicalIndex++;
                        }
                        MonitorApiHelper.DestroyPhysicalMonitors(numPhysicalMonitors, physicalMonitors);
                    }
                }
                return true;
            }, IntPtr.Zero);

            return monitors;
        }
    }
}
