using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace LuminaControl;

public interface IMonitor
{
    string Id { get; }
    string Name { get; }
    bool IsInternal { get; }
    int GetBrightness();
    void SetBrightness(int brightness);
}

public class InternalMonitor(string id, string name) : IMonitor
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public bool IsInternal => true;

    public int GetBrightness()
    {
        Logger.Log("InternalMonitor: GetBrightness starting WMI query...");
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery("WmiMonitorBrightness"));
            using var collection = searcher.Get();
            foreach (var o in collection)
            {
                if (o is not ManagementObject mObj) continue;
                
                var instanceName = mObj["InstanceName"]?.ToString();
                if (string.IsNullOrEmpty(Id) || instanceName == Id)
                {
                    int curBrightness = Convert.ToInt32(mObj["CurrentBrightness"]);
                    Logger.Log($"InternalMonitor: GetBrightness success, val = {curBrightness}");
                    return curBrightness;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"InternalMonitor: GetBrightness FAILED: {ex.Message}");
        }
        return 50; // Fallback
    }

    public void SetBrightness(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        Logger.Log($"InternalMonitor: SetBrightness to {brightness} starting WMI query...");
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery("WmiMonitorBrightnessMethods"));
            using var collection = searcher.Get();
            foreach (var o in collection)
            {
                if (o is not ManagementObject mObj) continue;

                var instanceName = mObj["InstanceName"]?.ToString();
                if (string.IsNullOrEmpty(Id) || instanceName == Id)
                {
                    Logger.Log($"InternalMonitor: WmiSetBrightness WMI method invoking for value {brightness}...");
                    mObj.InvokeMethod("WmiSetBrightness", [uint.MaxValue, (byte)brightness]);
                    Logger.Log("InternalMonitor: WmiSetBrightness WMI method returned");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"InternalMonitor: SetBrightness FAILED: {ex.Message}");
        }
    }
}

public class ExternalMonitor(string id, string name, int index) : IMonitor
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public bool IsInternal => false;
    private readonly int _index = index;

    public int GetBrightness()
    {
        Logger.Log($"ExternalMonitor({Name}): GetBrightness start");
        int currentBrightness = 50;
        MonitorApiHelper.OperateOnPhysicalMonitor(_index, (hPhys) =>
        {
            Logger.Log($"ExternalMonitor({Name}): calling GetMonitorBrightness API...");
            if (MonitorApiHelper.GetMonitorBrightness(hPhys, out _, out var cur, out _))
            {
                currentBrightness = (int)cur;
                Logger.Log($"ExternalMonitor({Name}): GetMonitorBrightness API success, val = {cur}");
            }
            else
            {
                Logger.Log($"ExternalMonitor({Name}): GetMonitorBrightness API FAILED");
            }
        });
        return currentBrightness;
    }

    public void SetBrightness(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        Logger.Log($"ExternalMonitor({Name}): SetBrightness to {brightness} start");
        MonitorApiHelper.OperateOnPhysicalMonitor(_index, (hPhys) =>
        {
            Logger.Log($"ExternalMonitor({Name}): calling SetMonitorBrightness API for value {brightness}...");
            bool success = MonitorApiHelper.SetMonitorBrightness(hPhys, (uint)brightness);
            Logger.Log($"ExternalMonitor({Name}): SetMonitorBrightness API returned, success = {success}");
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

    [DllImport("dxva2.dll", EntryPoint = "GetNumberOfPhysicalMonitorsFromHMONITOR", SetLastError = true)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR", SetLastError = true)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitors", SetLastError = true)]
    public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", EntryPoint = "GetMonitorBrightness", SetLastError = true)]
    public static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", EntryPoint = "SetMonitorBrightness", SetLastError = true)]
    public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    public delegate void PhysicalMonitorAction(IntPtr hPhysicalMonitor);

    public static void OperateOnPhysicalMonitor(int targetIndex, PhysicalMonitorAction action)
    {
        int currentIndex = 0;
        bool found = false;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            if (found) return true;

            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var numPhysicalMonitors) && numPhysicalMonitors > 0)
            {
                var physicalMonitors = new PHYSICAL_MONITOR[numPhysicalMonitors];
                if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numPhysicalMonitors, physicalMonitors))
                {
                    for (int i = 0; i < numPhysicalMonitors; i++)
                    {
                        if (currentIndex == targetIndex)
                        {
                            Logger.Log($"MonitorApiHelper: invoking delegate action for physical monitor handle {physicalMonitors[i].hPhysicalMonitor}...");
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
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\wmi"), new SelectQuery("WmiMonitorBrightness"));
            using var collection = searcher.Get();
            foreach (var o in collection)
            {
                if (o is not ManagementObject mObj) continue;
                
                var instanceName = mObj["InstanceName"]?.ToString() ?? string.Empty;
                var friendlyName = "Встроенный экран";
                monitors.Add(new InternalMonitor(instanceName, friendlyName));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"WMI Monitor discovery FAILED: {ex.Message}");
        }

        // 2. Discover external monitors via DXVA2 Physical Monitor API
        try
        {
            int index = 0;
            MonitorApiHelper.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref MonitorApiHelper.RECT lprcMonitor, IntPtr dwData)
            {
                if (MonitorApiHelper.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var numPhysicalMonitors) && numPhysicalMonitors > 0)
                {
                    var physicalMonitors = new MonitorApiHelper.PHYSICAL_MONITOR[numPhysicalMonitors];
                    if (MonitorApiHelper.GetPhysicalMonitorsFromHMONITOR(hMonitor, numPhysicalMonitors, physicalMonitors))
                    {
                        for (int i = 0; i < numPhysicalMonitors; i++)
                        {
                            var description = physicalMonitors[i].szPhysicalMonitorDescription;
                            if (string.IsNullOrEmpty(description))
                            {
                                description = $"Внешний монитор {index + 1}";
                            }
                            
                            // Check if monitor actually supports brightness APIs
                            if (MonitorApiHelper.GetMonitorBrightness(physicalMonitors[i].hPhysicalMonitor, out _, out _, out _))
                            {
                                monitors.Add(new ExternalMonitor(
                                    $"External_{index}_{i}",
                                    description,
                                    index
                                ));
                            }
                            index++;
                        }
                        MonitorApiHelper.DestroyPhysicalMonitors(numPhysicalMonitors, physicalMonitors);
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Logger.Log($"DDC/CI Monitor discovery FAILED: {ex.Message}");
        }

        return monitors;
    }
}
