@echo off
setlocal
cd /d "%~dp0"

echo ======================================================
echo             RyzerDNS Client - Launching
echo ======================================================
echo.

set "DOTNET_ROOT=C:\Users\Pourya\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

if exist "last\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS (Final Version - last)...
    start "" "last\RyzerDNS.exe"
) else if exist "publish_v13\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v13\RyzerDNS.exe"
) else if exist "publish_v12\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v12\RyzerDNS.exe"
) else if exist "publish_v11\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v11\RyzerDNS.exe"
) else if exist "publish_v10\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v10\RyzerDNS.exe"
) else if exist "publish_v9\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v9\RyzerDNS.exe"
) else if exist "publish_v8\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v8\RyzerDNS.exe"
) else if exist "publish_v7\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v7\RyzerDNS.exe"
) else if exist "publish_v6\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v6\RyzerDNS.exe"
) else if exist "publish_v5\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS Cyber-Glass Edition...
    start "" "publish_v4\RyzerDNS.exe"
) else if exist "publish_v3\RyzerDNS.exe" (
    echo [INFO] Starting RyzerDNS...
    start "" "publish_v3\RyzerDNS.exe"
) else (
    echo [INFO] Starting RyzerDNS...
    start "" "bin\Release\net8.0-windows\RyzerDNS.exe"
)
