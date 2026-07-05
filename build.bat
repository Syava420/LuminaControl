@echo off
echo ===================================================
echo Compiling LuminaControl...
echo ===================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set OUT="C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\LuminaControl.exe"

set REFS=/r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll"

set FILES="C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\Program.cs" "C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\MonitorManager.cs" "C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\MainWindow.cs" "C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\Styles.cs" "C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\ModernSlider.cs" "C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\Logger.cs"

%CSC% /target:winexe /win32icon:"C:\Users\Neuron\.gemini\antigravity\scratch\LuminaControl\app.ico" /out:%OUT% %REFS% %FILES%

if %ERRORLEVEL% equ 0 (
    echo ===================================================
    echo Compilation SUCCESSFUL!
    echo Executable created: LuminaControl.exe
    echo ===================================================
) else (
    echo ===================================================
    echo Compilation FAILED!
    echo ===================================================
    pause
)
