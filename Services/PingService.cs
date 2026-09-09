using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace RyzerDNS.Services
{
    public class PingService
    {
        public async Task<long?> MeasureLatencyAsync(string ipAddress, int timeoutMs = 2000)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, timeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    return reply.RoundtripTime;
                }
            }
            catch
            {
                // Ping failed or blocked by local firewall/ISP
            }
            return null;
        }
    }
}
