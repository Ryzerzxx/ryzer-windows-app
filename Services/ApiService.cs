using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using RyzerDNS.Models;

namespace RyzerDNS.Services
{
    public class ApiService
    {
        private const string BaseUrl = "https://ryzerdns.ir";
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RyzerDNS-DesktopClient/1.0");
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public async Task<(bool success, LoginResponse? response, string? errorMessage)> LoginAsync(string username, string password)
        {
            try
            {
                var payload = new LoginRequest
                {
                    Username = username.Trim(),
                    Password = password
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/user/login", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<LoginResponse>(responseBody, _jsonOptions);
                    if (result != null && result.IsSuccess && !string.IsNullOrEmpty(result.Token))
                    {
                        return (true, result, null);
                    }

                    string err = result?.Message ?? result?.Detail ?? "نام کاربری یا کلمه عبور نادرست است.";
                    return (false, result, err);
                }
                else
                {
                    // Attempt 1: Deserialize to LoginResponse to read message or detail
                    try
                    {
                        var errObj = JsonSerializer.Deserialize<LoginResponse>(responseBody, _jsonOptions);
                        string? errorText = errObj?.Message ?? errObj?.Detail;
                        if (!string.IsNullOrEmpty(errorText))
                        {
                            return (false, null, errorText);
                        }
                    }
                    catch { }

                    // Attempt 2: Directly inspect JSON document for "detail" or "message"
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("detail", out var detailElem))
                        {
                            if (detailElem.ValueKind == JsonValueKind.String)
                            {
                                return (false, null, detailElem.GetString());
                            }
                            else if (detailElem.ValueKind == JsonValueKind.Array && detailElem.GetArrayLength() > 0)
                            {
                                var first = detailElem[0];
                                if (first.TryGetProperty("msg", out var msgElem))
                                {
                                    return (false, null, msgElem.GetString());
                                }
                            }
                        }
                        if (doc.RootElement.TryGetProperty("message", out var msgElem2) && msgElem2.ValueKind == JsonValueKind.String)
                        {
                            return (false, null, msgElem2.GetString());
                        }
                    }
                    catch { }

                    return (false, null, $"خطای سرور با کد {(int)response.StatusCode}. لطفاً دوباره تلاش کنید.");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "مهلت ارتباط با سرور به پایان رسید (Timeout). اتصال اینترنت خود را بررسی نمایید.");
            }
            catch (HttpRequestException ex)
            {
                return (false, null, $"خطا در اتصال به سرور: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"خطای غیرمنتظره: {ex.Message}");
            }
        }

        public async Task<(bool success, ProfileResponse? profile, string? errorMessage, bool isTokenInvalid)> GetProfileAsync(string token)
        {
            try
            {
                string url = $"/api/user/profile?token={Uri.EscapeDataString(token)}";
                var response = await _httpClient.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var profile = JsonSerializer.Deserialize<ProfileResponse>(responseBody, _jsonOptions);
                    if (profile != null && profile.IsSuccess)
                    {
                        return (true, profile, null, false);
                    }
                    bool isInvalid = responseBody.Contains("token") || responseBody.Contains("منقضی") || responseBody.Contains("invalid");
                    return (false, profile, profile?.Message ?? profile?.Detail ?? "خطا در بازیابی اطلاعات کاربر.", isInvalid);
                }
                else
                {
                    bool is401or403 = response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden;
                    try
                    {
                        var errObj = JsonSerializer.Deserialize<ProfileResponse>(responseBody, _jsonOptions);
                        if (!string.IsNullOrEmpty(errObj?.Message))
                            return (false, null, errObj.Message, is401or403);
                    }
                    catch { }

                    return (false, null, $"خطای سرور ({response.StatusCode}) در دریافت پروفایل.", is401or403);
                }
            }
            catch (Exception ex)
            {
                // Network glitch, DNS resolving, timeout -> token is NOT invalid!
                return (false, null, $"خطا در دریافت پروفایل: {ex.Message}", false);
            }
        }

        public async Task<string?> DetectIpAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/user/detect-ip");
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("ip", out var ipElem))
                    {
                        string? ip = ipElem.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(ip)) return ip;
                    }
                }
            }
            catch { }
            return null;
        }

        public async Task<(bool success, string message, string? registeredIp)> RegisterIpAsync(string token, string? manualIp = null)
        {
            try
            {
                var payload = new RegisterIpRequest 
                { 
                    Token = token,
                    ManualIp = manualIp
                };
                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/user/register-ip", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var res = JsonSerializer.Deserialize<RegisterIpResponse>(responseBody, _jsonOptions);
                    if (res != null && res.IsSuccess)
                    {
                        return (true, res.Message ?? res.Detail ?? "آدرس آی‌پی با موفقیت در فایروال سرور ثبت شد.", res.RegisteredIp);
                    }
                    return (false, res?.Message ?? res?.Detail ?? "خطا در ثبت آی‌پی توسط سرور.", null);
                }
                else
                {
                    try
                    {
                        var errObj = JsonSerializer.Deserialize<RegisterIpResponse>(responseBody, _jsonOptions);
                        if (!string.IsNullOrEmpty(errObj?.Message))
                            return (false, errObj.Message, null);
                    }
                    catch { }

                    return (false, $"خطای سرور ({response.StatusCode}) در ثبت آی‌پی.", null);
                }
            }
            catch (Exception ex)
            {
                return (false, $"خطا در اتصال به سرور برای ثبت آی‌پی: {ex.Message}", null);
            }
        }

        public async Task<AppConfigResponse> GetRemoteConfigAsync()
        {
            var fallback = new AppConfigResponse
            {
                Status = "fallback",
                PrimaryDns = "87.248.145.40",
                SecondaryDns = "46.8.102.223",
                UpdateAvailable = false
            };

            try
            {
                var response = await _httpClient.GetAsync("/api/app/config");
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var config = JsonSerializer.Deserialize<AppConfigResponse>(responseBody, _jsonOptions);
                    if (config != null && config.IsSuccess)
                    {
                        if (string.IsNullOrWhiteSpace(config.PrimaryDns))
                            config.PrimaryDns = fallback.PrimaryDns;
                        if (string.IsNullOrWhiteSpace(config.SecondaryDns))
                            config.SecondaryDns = fallback.SecondaryDns;

                        return config;
                    }
                }
            }
            catch
            {
                // Fallback gracefully to offline defaults
            }

            return fallback;
        }

        public async Task<(string? authUrl, string? stateKey)> StartGoogleAuthAsync()
        {
            try
            {
                var payload = new { mode = "login", init_data = "windows_client" };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/google/miniapp-init", content);
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("state", out var stateElem) &&
                        doc.RootElement.TryGetProperty("client_id", out var clientElem))
                    {
                        string state = stateElem.GetString() ?? "";
                        string clientId = clientElem.GetString() ?? "";
                        string redirectUri = Uri.EscapeDataString("https://ryzerdns.ir/auth/google/callback");
                        string scope = Uri.EscapeDataString("openid email profile");
                        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope={scope}&state={Uri.EscapeDataString(state)}&prompt=select_account";
                        return (authUrl, state);
                    }
                }
            }
            catch { }
            return (null, null);
        }

        public async Task<(bool finished, bool success, string? token, string? username, string? error)> PollGoogleAuthAsync(string stateKey)
        {
            try
            {
                string url = $"/api/auth/google/poll?state={Uri.EscapeDataString(stateKey)}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    if (status == "success")
                    {
                        string token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
                        string user = doc.RootElement.TryGetProperty("web_username", out var u) ? u.GetString() ?? "" : "";
                        return (true, true, token, user, null);
                    }
                    else if (status == "error")
                    {
                        string msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "خطا در احراز هویت با گوگل";
                        return (true, false, null, null, msg);
                    }
                }
            }
            catch { }
            return (false, false, null, null, null);
        }
    }
}
