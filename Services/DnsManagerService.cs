using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RyzerDNS.Services
{
    public class DnsManagerService
    {
        public static string PrimaryDns { get; set; } = "87.248.145.40";
        public static string SecondaryDns { get; set; } = "46.8.102.223";
        public static string DohUrl { get; set; } = "https://ryzerdns.ir/dns-query";
        public static string DotHostname { get; set; } = "dns.ryzerdns.ir";

        /// <summary>
        /// Retrieves all currently active and internet-connected network interfaces.
        /// </summary>
        public List<NetworkInterface> GetActiveNetworkInterfaces()
        {
            var result = new List<NetworkInterface>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    // Exclude common virtual adapters unless no other adapter is available
                    string desc = ni.Description.ToLowerInvariant();
                    if (desc.Contains("virtual") || desc.Contains("pseudo") || desc.Contains("vpn") ||
                        desc.Contains("hyper-v") || desc.Contains("vmware") || desc.Contains("tap-windows") ||
                        desc.Contains("wsl") || desc.Contains("npcap"))
                    {
                        continue;
                    }

                    var ipProps = ni.GetIPProperties();
                    var ipv4Props = ipProps.GetIPv4Properties();
                    if (ipv4Props == null)
                        continue;

                    // Must have a valid IPv4 gateway
                    var gateways = ipProps.GatewayAddresses
                        .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.ToString().Equals("0.0.0.0"))
                        .ToList();

                    if (gateways.Any())
                    {
                        result.Add(ni);
                    }
                }

                // If filtering excluded everything, fall back to any UP non-loopback with gateway
                if (result.Count == 0)
                {
                    foreach (var ni in interfaces)
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            var ipProps = ni.GetIPProperties();
                            if (ipProps.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                            {
                                result.Add(ni);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // In case of WMI/NetworkInterface exceptions
            }

            return result;
        }

        /// <summary>
        /// Returns the name of the primary active adapter (e.g. Ethernet or Wi-Fi).
        /// </summary>
        public string? GetPrimaryAdapterName()
        {
            var active = GetActiveNetworkInterfaces();
            return active.FirstOrDefault()?.Name;
        }

        /// <summary>
        /// Checks whether RyzerDNS (87.248.145.40) is currently assigned on any active adapter.
        /// </summary>
        public bool IsRyzerDnsActive()
        {
            try
            {
                var interfaces = GetActiveNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    var dnsAddresses = ni.GetIPProperties().DnsAddresses;
                    foreach (var ip in dnsAddresses)
                    {
                        string ipStr = ip.ToString();
                        if (ipStr == PrimaryDns || ipStr == SecondaryDns || ipStr == "87.248.145.40" || ipStr == "46.8.102.223")
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Sets Ryzer DNS on all active internet network adapters.
        /// </summary>
        public async Task<(bool success, string message)> SetRyzerDnsAsync()
        {
            var adapters = GetActiveNetworkInterfaces();
            if (adapters.Count == 0)
            {
                return (false, "هیچ کارت شبکه فعالی متصل به اینترنت یافت نشد.");
            }

            bool anySuccess = false;
            string lastError = "";

            foreach (var adapter in adapters)
            {
                string name = adapter.Name;
                try
                {
                    // 1. Set Primary Static DNS
                    var res1 = await RunCommandAsync("netsh", $"interface ipv4 set dnsservers name=\"{name}\" source=static address={PrimaryDns} register=both");
                    if (res1.exitCode != 0)
                    {
                        lastError = res1.output;
                        continue;
                    }

                    // 2. Add Secondary DNS
                    var res2 = await RunCommandAsync("netsh", $"interface ipv4 add dnsservers name=\"{name}\" address={SecondaryDns} index=2");

                    anySuccess = true;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            // Flush DNS
            await FlushDnsAsync();

            if (anySuccess)
            {
                return (true, "دی‌ان‌اس با موفقیت بر روی کارت شبکه تنظیم شد.");
            }
            else
            {
                return (false, $"خطا در تنظیم DNS: {lastError}. مطمئن شوید برنامه با دسترسی Administrator اجرا شده است.");
            }
        }

        /// <summary>
        /// Resets DNS to DHCP (Automatic) on all active network adapters.
        /// </summary>
        public async Task<(bool success, string message)> ResetDnsToDhcpAsync()
        {
            var adapters = GetActiveNetworkInterfaces();
            if (adapters.Count == 0)
            {
                // Fallback to resetting Ethernet and Wi-Fi by name
                await RunCommandAsync("netsh", "interface ipv4 set dnsservers name=\"Ethernet\" source=dhcp");
                await RunCommandAsync("netsh", "interface ipv4 set dnsservers name=\"Wi-Fi\" source=dhcp");
                await FlushDnsAsync();
                return (true, "تنظیمات دی‌ان‌اس به حالت خودکار (DHCP) بازگردانده شد.");
            }

            foreach (var adapter in adapters)
            {
                try
                {
                    await RunCommandAsync("netsh", $"interface ipv4 set dnsservers name=\"{adapter.Name}\" source=dhcp");
                }
                catch
                {
                }
            }

            await FlushDnsAsync();
            return (true, "تنظیمات دی‌ان‌اس با موفقیت به حالت پیش‌فرض و خودکار (DHCP) بازگشت.");
        }

        /// <summary>
        /// Flushes the Windows DNS resolver cache.
        /// </summary>
        public async Task<bool> FlushDnsAsync()
        {
            try
            {
                var result = await RunCommandAsync("ipconfig", "/flushdns");
                return result.exitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Synchronously and rapidly resets DNS on all network adapters to DHCP (automatic).
        /// Designed specifically for system shutdown, restart, and sudden termination.
        /// Executes netsh in parallel across all active adapters for sub-second completion.
        /// </summary>
        public static void ResetDnsToDhcpFastSync()
        {
            try
            {
                var adapterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    foreach (var ni in interfaces)
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            adapterNames.Add(ni.Name);
                        }
                    }
                }
                catch { }

                // Always include common default adapter names as reliable fallbacks
                adapterNames.Add("Ethernet");
                adapterNames.Add("Wi-Fi");

                // Execute netsh commands concurrently for ultra-fast execution (< 500ms)
                var runningProcesses = new List<Process>();
                foreach (string name in adapterNames)
                {
                    try
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = $"interface ipv4 set dnsservers name=\"{name}\" source=dhcp",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        if (p.Start())
                        {
                            runningProcesses.Add(p);
                        }
                    }
                    catch { }
                }

                // Wait up to 1.5s for all adapter resets to finish
                foreach (var p in runningProcesses)
                {
                    try
                    {
                        p.WaitForExit(1500);
                        p.Dispose();
                    }
                    catch { }
                }

                // Rapid flush DNS
                try
                {
                    using var pFlush = new Process();
                    pFlush.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ipconfig",
                        Arguments = "/flushdns",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    pFlush.Start();
                    pFlush.WaitForExit(1000);
                }
                catch { }
            }
            catch { }
        }

        private static Task<(int exitCode, string output)> RunCommandAsync(string fileName, string arguments)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var process = new Process();
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.Default,
                        StandardErrorEncoding = System.Text.Encoding.Default
                    };

                    process.Start();
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);

                    string combined = (stdout + " " + stderr).Trim();
                    return (process.ExitCode, combined);
                }
                catch (Exception ex)
                {
                    return (-1, ex.Message);
                }
            });
        }
    }
}
