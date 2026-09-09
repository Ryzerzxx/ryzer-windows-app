using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using RyzerDNS.Models;

namespace RyzerDNS.Services
{
    public class UpdateManagerService
    {
        private readonly HttpClient _httpClient;

        public UpdateManagerService()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(3)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RyzerDNS-DesktopClient/1.0");
        }

        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(1, 0, 0);
            }
        }

        public static bool IsNewerVersion(string? serverVersionStr)
        {
            if (string.IsNullOrWhiteSpace(serverVersionStr))
                return false;

            string cleaned = serverVersionStr.Trim().TrimStart('v', 'V');
            if (Version.TryParse(cleaned, out var serverVersion))
            {
                var current = CurrentVersion;
                return serverVersion > current;
            }

            var parts = cleaned.Split('.');
            if (parts.Length == 1 && int.TryParse(parts[0], out int major))
            {
                return new Version(major, 0, 0) > CurrentVersion;
            }

            return false;
        }

        public async Task<bool> CheckAndDownloadUpdateAsync(AppConfigResponse config)
        {
            if (config == null || !config.UpdateAvailable || string.IsNullOrWhiteSpace(config.UpdateUrl))
            {
                return false;
            }

            bool isNewer = IsNewerVersion(config.LatestVersion);
            if (!isNewer && !config.UpdateAvailable)
            {
                return false;
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string newExePath = Path.Combine(baseDir, "RyzerDNS.new.exe");
                string tempFilePath = Path.Combine(baseDir, "RyzerDNS.new.tmp");

                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }

                using var response = await _httpClient.GetAsync(config.UpdateUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                await using (var remoteStream = await response.Content.ReadAsStreamAsync())
                await using (var localFileStream = File.Create(tempFilePath))
                {
                    await remoteStream.CopyToAsync(localFileStream);
                }

                var fileInfo = new FileInfo(tempFilePath);
                if (fileInfo.Length < 50 * 1024)
                {
                    try { File.Delete(tempFilePath); } catch { }
                    return false;
                }

                if (File.Exists(newExePath))
                {
                    try { File.Delete(newExePath); } catch { }
                }

                File.Move(tempFilePath, newExePath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManagerService] Download error: {ex.Message}");
                return false;
            }
        }

        public void ApplyUpdateAndRestart()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string newExe = Path.Combine(baseDir, "RyzerDNS.new.exe");

                if (!File.Exists(newExe))
                {
                    return;
                }

                string currentExe = Path.Combine(baseDir, "RyzerDNS.exe");

                string tempUpdaterScript = Path.Combine(Path.GetTempPath(), "RyzerDNS_Updater.bat");
                int currentPid = Environment.ProcessId;

                string scriptContent = $@"@echo off
setlocal
:wait_process
tasklist /fi ""PID eq {currentPid}"" 2>nul | find ""{currentPid}"" >nul
if %ERRORLEVEL% equ 0 (
    timeout /t 1 /nobreak >nul
    goto wait_process
)
if exist ""{newExe}"" (
    copy /y ""{newExe}"" ""{currentExe}"" >nul
    del /f /q ""{newExe}"" >nul
)
start """" ""{currentExe}""
del ""%~f0"" >nul 2>&1
";

                File.WriteAllText(tempUpdaterScript, scriptContent, System.Text.Encoding.ASCII);

                var psi = new ProcessStartInfo
                {
                    FileName = tempUpdaterScript,
                    WorkingDirectory = baseDir,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateManagerService] Restart error: {ex.Message}");
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
        }
    }
}
