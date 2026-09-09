using System;
using System.Text.Json.Serialization;

namespace RyzerDNS.Models
{
    public class LoginRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("web_username")]
        public string? WebUsername { get; set; }

        private string? _message;
        [JsonPropertyName("message")]
        public string? Message
        {
            get => !string.IsNullOrEmpty(_message) ? _message : Detail;
            set => _message = value;
        }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonIgnore]
        public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
    }

    public class ProfileResponse
    {
        [JsonPropertyName("plan_tier")]
        public string? PlanTier { get; set; }

        [JsonPropertyName("plan_expires_at")]
        public string? PlanExpiresAt { get; set; }

        [JsonPropertyName("registered_ip")]
        public string? RegisteredIp { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        private string? _message;
        [JsonPropertyName("message")]
        public string? Message
        {
            get => !string.IsNullOrEmpty(_message) ? _message : Detail;
            set => _message = value;
        }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonIgnore]
        public bool IsSuccess => !string.IsNullOrEmpty(PlanTier) || string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
    }

    public class RegisterIpRequest
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    public class RegisterIpResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        private string? _message;
        [JsonPropertyName("message")]
        public string? Message
        {
            get => !string.IsNullOrEmpty(_message) ? _message : Detail;
            set => _message = value;
        }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonIgnore]
        public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
    }

    public class AppSettings
    {
        public string? Token { get; set; }
        public string? Username { get; set; }
        public bool RememberMe { get; set; } = true;
        public bool AutoConnectOnLaunch { get; set; } = false;
        public string? LastAdapterName { get; set; }
        public ProfileResponse? CachedProfile { get; set; }
    }

    public class AppConfigResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("primary_dns")]
        public string PrimaryDns { get; set; } = "87.248.145.40";

        [JsonPropertyName("secondary_dns")]
        public string SecondaryDns { get; set; } = "46.8.102.223";

        [JsonPropertyName("latest_version")]
        public string? LatestVersion { get; set; }

        [JsonPropertyName("update_available")]
        public bool UpdateAvailable { get; set; } = false;

        [JsonPropertyName("update_url")]
        public string? UpdateUrl { get; set; }

        [JsonIgnore]
        public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
    }
}
