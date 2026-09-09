@echo off
setlocal
cd /d "%~dp0"

echo ======================================================
echo             RyzerDNS Client - Build
echo ======================================================
echo.

set "DOTNET_ROOT=C:\Users\Pourya\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

"%DOTNET_ROOT%\dotnet.exe" publish -c Release -o "%~dp0last"

if %ERRORLEVEL% equ 0 (
    if exist "%~dp0Assets" (
        xcopy /E /I /Y "%~dp0Assets" "%~dp0last\Assets" >nul
    )
    echo.
    echo ======================================================
    echo   [SUCCESS] Application built successfully!
    echo   Executable: last\RyzerDNS.exe
    echo ======================================================
) else (
    echo.
    echo [ERROR] Build encountered an error.
)

pause
