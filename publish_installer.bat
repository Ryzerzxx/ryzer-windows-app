@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ======================================================================
echo              RyzerDNS Client - Full Publish ^& Setup Builder
echo ======================================================================
echo.

:: 1. Ensure .NET SDK environment is configured
if not defined DOTNET_ROOT (
    if exist "C:\Users\Pourya\AppData\Local\Microsoft\dotnet\dotnet.exe" (
        set "DOTNET_ROOT=C:\Users\Pourya\AppData\Local\Microsoft\dotnet"
    )
)
if defined DOTNET_ROOT (
    set "PATH=%DOTNET_ROOT%;%PATH%"
)

:: 2. Publish Self-Contained Win-x64 Application
echo [1/3] Publishing Self-Contained .NET 8 Win-x64 Application...
echo Command: dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./publish
echo.

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./publish
if %ERRORLEVEL% neq 0 (
    if defined DOTNET_ROOT (
        "%DOTNET_ROOT%\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./publish
    )
)

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] dotnet publish failed. Please check build errors above.
    pause
    exit /b %ERRORLEVEL%
)

:: 3. Copy runtime Assets
echo.
echo [2/3] Verifying runtime Assets folder...
if exist "Assets" (
    if not exist "publish\Assets" mkdir "publish\Assets"
    xcopy /E /I /Y "Assets" "publish\Assets" >nul
)

:: 4. Locate Inno Setup Compiler (ISCC.exe)
echo.
echo [3/3] Locating Inno Setup Compiler ^(ISCC.exe^)...

set "ISCC_EXE="

where iscc.exe >nul 2>&1
if %ERRORLEVEL% equ 0 (
    set "ISCC_EXE=iscc.exe"
)

if not defined ISCC_EXE (
    if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    )
)
if not defined ISCC_EXE (
    if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=%ProgramFiles%\Inno Setup 6\ISCC.exe"
    )
)
if not defined ISCC_EXE (
    if exist "%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe" (
        set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe"
    )
)
if not defined ISCC_EXE (
    if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
    )
)

if not exist "Output" mkdir "Output"

if defined ISCC_EXE goto :RunInnoSetup
goto :InnoNotFound

:RunInnoSetup
echo Found Inno Setup Compiler: "!ISCC_EXE!"
echo Running Inno Setup compilation for RyzerDNS-Setup.exe...
echo.
"!ISCC_EXE!" "%~dp0installer.iss"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ======================================================================
    echo   [SUCCESS] Setup Installer built successfully!
    echo   Installer Path: "%~dp0Output\RyzerDNS-Setup.exe"
    echo ======================================================================
) else (
    echo.
    echo [ERROR] Inno Setup compilation encountered an error.
)
goto :EndScript

:InnoNotFound
echo.
echo ======================================================================
echo [NOTICE] Inno Setup Compiler ^(ISCC.exe^) was not found on this system.
echo - The application has been fully published to: "%~dp0publish"
echo - To generate "RyzerDNS-Setup.exe", please install Inno Setup from:
echo   https://jrsoftware.org/isdl.php
echo - After installing, simply run this script again or right-click
echo   "installer.iss" and select "Compile".
echo ======================================================================
goto :EndScript

:EndScript
echo.
pause
