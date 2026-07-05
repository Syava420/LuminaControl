@echo off
setlocal

:: Find dotnet in path, or use our user-installed dotnet
where dotnet >nul 2>nul
if %ERRORLEVEL% equ 0 (
    set DOTNET=dotnet
) else (
    if exist "C:\Users\Neuron\dotnet\dotnet.exe" (
        set DOTNET="C:\Users\Neuron\dotnet\dotnet.exe"
    ) else (
        echo Error: dotnet SDK not found! Please install .NET SDK or run dotnet-install script.
        pause
        exit /b 1
    )
)

echo ===================================================
echo Building LuminaControl on modern .NET...
echo ===================================================
%DOTNET% build -c Release

if %ERRORLEVEL% equ 0 (
    echo ===================================================
    echo Publishing self-contained single-file executable...
    echo ===================================================
    %DOTNET% publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:PublishReadyToRun=true --self-contained true
    
    if %ERRORLEVEL% equ 0 (
        echo ===================================================
        echo Success! Executable created in:
        echo bin\Release\net8.0-windows\win-x64\publish\LuminaControl.exe
        echo ===================================================
    ) else (
        echo Publish FAILED!
    )
) else (
    echo Build FAILED!
)

pause
