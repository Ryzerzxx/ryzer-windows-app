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

        [JsonPropertyName("manual_ip")]
        public string? ManualIp { get; set; }
    }

    public class RegisterIpResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("registered_ip")]
        public string? RegisteredIp { get; set; }

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
        public string SecondaryDns { get; set; } = "193.242.208.98";

        [JsonPropertyName("latest_version")]
        public string? LatestVersion { get; set; }

        [JsonPropertyName("update_available")]
        public bool UpdateAvailable { get; set; } = false;

        [JsonPropertyName("update_url")]
        public string? UpdateUrl { get; set; }

        [JsonPropertyName("announcement")]
        public string? Announcement { get; set; }

        [JsonPropertyName("ui_config")]
        public AppUiConfig? UiConfig { get; set; }

        [JsonIgnore]
        public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
    }

    public class AppUiConfig
    {
        [JsonPropertyName("about_title")]
        public string? AboutTitle { get; set; }

        [JsonPropertyName("about_message")]
        public string? AboutMessage { get; set; }

        [JsonPropertyName("about_button_text")]
        public string? AboutButtonText { get; set; }

        [JsonPropertyName("connect_btn_text")]
        public string? ConnectBtnText { get; set; }

        [JsonPropertyName("disconnect_btn_text")]
        public string? DisconnectBtnText { get; set; }

        [JsonPropertyName("status_connected_text")]
        public string? StatusConnectedText { get; set; }

        [JsonPropertyName("status_disconnected_text")]
        public string? StatusDisconnectedText { get; set; }

        [JsonPropertyName("website_url")]
        public string? WebsiteUrl { get; set; }

        [JsonPropertyName("telegram_channel_url")]
        public string? TelegramChannelUrl { get; set; }

        [JsonPropertyName("telegram_bot_url")]
        public string? TelegramBotUrl { get; set; }

        [JsonPropertyName("support_url")]
        public string? SupportUrl { get; set; }

        [JsonPropertyName("banner_enabled")]
        public bool BannerEnabled { get; set; } = false;

        [JsonPropertyName("banner_text")]
        public string? BannerText { get; set; }
    }
}
