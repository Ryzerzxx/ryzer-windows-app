using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Interop;
using Path = System.Windows.Shapes.Path;
using RyzerDNS.Models;
using RyzerDNS.Services;

namespace RyzerDNS
{
    public partial class MainWindow : Window
    {
        private static readonly ImageSource? ConnectedOverlayIcon = CreateConnectedOverlayIcon();
        private static readonly SolidColorBrush BrushAccentRed = new SolidColorBrush(Color.FromRgb(255, 59, 92));
        private static readonly SolidColorBrush BrushAccentRedGlow = new SolidColorBrush(Color.FromArgb(38, 255, 59, 92));
        private static readonly SolidColorBrush BrushAccentGreen = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        private static readonly SolidColorBrush BrushAccentGreenGlow = new SolidColorBrush(Color.FromArgb(38, 0, 230, 118));
        private static readonly SolidColorBrush BrushAccentYellow = new SolidColorBrush(Color.FromRgb(255, 179, 0));
        private static readonly SolidColorBrush BrushAccentYellowGlow = new SolidColorBrush(Color.FromArgb(38, 255, 179, 0));
        private static readonly SolidColorBrush BrushTextSecondary = new SolidColorBrush(Color.FromRgb(142, 142, 168));
        private static readonly SolidColorBrush BrushTextMuted = new SolidColorBrush(Color.FromRgb(90, 90, 114));

        private static readonly SolidColorBrush BrushBadgeGreenBg = new SolidColorBrush(Color.FromArgb(26, 0, 230, 118));
        private static readonly SolidColorBrush BrushBadgeGreenBorder = new SolidColorBrush(Color.FromArgb(102, 0, 230, 118));
        private static readonly SolidColorBrush BrushBadgeGreenText = new SolidColorBrush(Color.FromRgb(0, 230, 118));

        private static readonly SolidColorBrush BrushBadgeOrangeBg = new SolidColorBrush(Color.FromArgb(26, 255, 179, 0));
        private static readonly SolidColorBrush BrushBadgeOrangeBorder = new SolidColorBrush(Color.FromArgb(102, 255, 179, 0));
        private static readonly SolidColorBrush BrushBadgeOrangeText = new SolidColorBrush(Color.FromRgb(255, 179, 0));

        private static readonly SolidColorBrush BrushBadgeBlueBg = new SolidColorBrush(Color.FromArgb(26, 0, 229, 255));
        private static readonly SolidColorBrush BrushBadgeBlueBorder = new SolidColorBrush(Color.FromArgb(102, 0, 229, 255));
        private static readonly SolidColorBrush BrushBadgeBlueText = new SolidColorBrush(Color.FromRgb(0, 229, 255));

        private static readonly SolidColorBrush BrushBadgeGrayBg = new SolidColorBrush(Color.FromArgb(13, 255, 255, 255));
        private static readonly SolidColorBrush BrushBadgeGrayBorder = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
        private static readonly SolidColorBrush BrushBadgeGrayText = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255));

        private readonly ApiService _apiService;
        private readonly StorageService _storageService;
        private readonly DnsManagerService _dnsService;
        private readonly PingService _pingService;
        private readonly UpdateManagerService _updateManagerService;

        private AppSettings _settings;
        private ProfileResponse? _currentProfile;
        private AppConfigResponse? _remoteConfig;
        private bool _isConnected = false;
        private bool _isProcessing = false;

        private string? _currentDetectedIp;
        private string? _currentRegisteredIp;
        private bool _isDetectingIp;
        private bool _isRegisteringIp;
        private System.Windows.Threading.DispatcherTimer? _ipCheckTimer;

        public MainWindow()
        {
            InitializeComponent();

            _apiService = new ApiService();
            _storageService = new StorageService();
            _dnsService = new DnsManagerService();
            _pingService = new PingService();
            _updateManagerService = new UpdateManagerService();

            _settings = _storageService.LoadSettings();

            Loaded += MainWindow_Loaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        }

        private const int WM_QUERYENDSESSION = 0x0011;
        private const int WM_ENDSESSION = 0x0016;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_QUERYENDSESSION || msg == WM_ENDSESSION)
            {
                App.PerformShutdownDnsCleanup();
            }
            return IntPtr.Zero;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _ = FetchRemoteConfigAsync();
            _ = UpdatePingAsync();

            // Check if Ryzer DNS is already active on the machine
            bool dnsActive = _dnsService.IsRyzerDnsActive();
            SetConnectionUiState(dnsActive);

            // Start 1-minute periodic IP check timer
            _ipCheckTimer?.Stop();
            _ipCheckTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _ipCheckTimer.Tick += (s, ev) => _ = DetectAndSyncIpAsync(silent: true);
            _ipCheckTimer.Start();

            // Initial IP detection
            _ = DetectAndSyncIpAsync(silent: false);

            // Auto-login if token is saved (Persistent Login)
            if (!string.IsNullOrEmpty(_settings.Token))
            {
                // Instantly show dashboard with cached profile data
                if (_settings.CachedProfile != null)
                {
                    _currentProfile = _settings.CachedProfile;
                    ApplyProfileToUi(_currentProfile, _settings.Username ?? "کاربر گرامی");
                }
                else
                {
                    ApplyProfileToUi(new ProfileResponse(), _settings.Username ?? "کاربر گرامی");
                }
                ShowDashboard();

                // Silently refresh profile in background without logging user out on temporary network drops
                _ = Task.Run(async () =>
                {
                    var (success, profile, err, isTokenInvalid) = await _apiService.GetProfileAsync(_settings.Token);
                    if (success && profile != null)
                    {
                        _currentProfile = profile;
                        _settings.CachedProfile = profile;
                        _storageService.SaveSettings(_settings);

                        Dispatcher.Invoke(() =>
                        {
                            ApplyProfileToUi(profile, _settings.Username ?? "کاربر گرامی");
                        });
                    }
                    else if (isTokenInvalid)
                    {
                        // Token explicitly rejected by server
                        _storageService.ClearSession();
                        Dispatcher.Invoke(() =>
                        {
                            ShowLogin();
                            TxtLoginError.Text = "نشست قبلی منقضی شده است. لطفاً مجدداً وارد شوید.";
                            LoginErrorBorder.Visibility = Visibility.Visible;
                        });
                    }
                });

                return;
            }

            ShowLogin();
        }

        private async Task FetchRemoteConfigAsync()
        {
            try
            {
                var config = await _apiService.GetRemoteConfigAsync();
                if (config != null)
                {
                    _remoteConfig = config;

                    if (!string.IsNullOrWhiteSpace(config.PrimaryDns))
                    {
                        DnsManagerService.PrimaryDns = config.PrimaryDns.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(config.SecondaryDns))
                    {
                        DnsManagerService.SecondaryDns = config.SecondaryDns.Trim();
                    }

                    // Update dynamic UI state if needed
                    Dispatcher.Invoke(() =>
                    {
                        SetConnectionUiState(_isConnected);
                    });

                    // Refresh latency ping with the updated server IP
                    _ = UpdatePingAsync();

                    // Check if an update has already been downloaded and is waiting to be applied
                    string newExePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RyzerDNS.new.exe");
                    if (File.Exists(newExePath) && new FileInfo(newExePath).Length > 50 * 1024)
                    {
                        ShowUpdateBannerWithSlideAnimation();
                    }
                    else if (config.UpdateAvailable && !string.IsNullOrWhiteSpace(config.UpdateUrl))
                    {
                        // Silently download the new binary in background (Telegram Desktop style)
                        _ = Task.Run(async () =>
                        {
                            bool downloaded = await _updateManagerService.CheckAndDownloadUpdateAsync(config);
                            if (downloaded)
                            {
                                ShowUpdateBannerWithSlideAnimation();
                            }
                        });
                    }
                }
            }
            catch
            {
                // Offline fallback remains completely active
            }
        }

        private void BtnRestartUpdate_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _updateManagerService.ApplyUpdateAndRestart();
        }

        private void ShowUpdateBannerWithSlideAnimation()
        {
            Dispatcher.Invoke(() =>
            {
                if (UpdateBanner.Visibility == Visibility.Visible && UpdateBanner.Opacity > 0.8)
                    return;

                UpdateBanner.Visibility = Visibility.Visible;

                var slideAnim = new DoubleAnimation
                {
                    From = -30,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(450),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var fadeAnim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(450),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                BannerTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
                UpdateBanner.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            });
        }

        private void ShowLogin()
        {
            LoginPanel.Visibility = Visibility.Visible;
            DashboardPanel.Visibility = Visibility.Collapsed;
            BtnLogout.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(_settings.Username))
            {
                TxtUsername.Text = _settings.Username;
                TxtPassword.Focus();
            }
            else
            {
                TxtUsername.Focus();
            }
        }

        private void ShowDashboard()
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            DashboardPanel.Visibility = Visibility.Visible;
            BtnLogout.Visibility = Visibility.Visible;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetConnectionUiState(_isConnected);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private async Task UpdatePingAsync()
        {
            PingDot.Fill = BrushTextMuted;
            TxtPingValue.Text = "... ms";
            TxtPingValue.Foreground = BrushTextSecondary;

            long? latency = await _pingService.MeasureLatencyAsync(DnsManagerService.PrimaryDns);
            if (latency.HasValue)
            {
                TxtPingValue.Text = $"{latency.Value} ms";
                if (latency.Value < 80)
                {
                    PingDot.Fill = BrushAccentGreen;
                    TxtPingValue.Foreground = BrushAccentGreen;
                }
                else if (latency.Value < 150)
                {
                    PingDot.Fill = BrushAccentYellow;
                    TxtPingValue.Foreground = BrushAccentYellow;
                }
                else
                {
                    PingDot.Fill = BrushAccentRed;
                    TxtPingValue.Foreground = BrushAccentRed;
                }
            }
            else
            {
                TxtPingValue.Text = "خطا";
                PingDot.Fill = BrushAccentRed;
                TxtPingValue.Foreground = BrushAccentRed;
            }
        }

        private bool IsSubscriptionExpired(string? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(expiresAt))
                return false;

            if (DateTimeOffset.TryParse(expiresAt, out DateTimeOffset dto))
            {
                return dto < DateTimeOffset.Now;
            }
            if (DateTime.TryParse(expiresAt, out DateTime dt))
            {
                return dt < DateTime.Now;
            }
            return false;
        }

        private string FormatRemainingDays(string? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(expiresAt))
                return "اشتراک فعال";

            DateTime expiryDate;
            if (DateTimeOffset.TryParse(expiresAt, out DateTimeOffset dto))
            {
                expiryDate = dto.LocalDateTime;
            }
            else if (!DateTime.TryParse(expiresAt, out expiryDate))
            {
                return expiresAt;
            }

            var diff = expiryDate - DateTime.Now;
            int days = (int)Math.Ceiling(diff.TotalDays);
            if (days > 0)
            {
                return $"{days} روز باقیمانده";
            }
            else if (days == 0 && diff.TotalSeconds > 0)
            {
                return "آخرین روز اشتراک";
            }
            else
            {
                return "اشتراک منقضی شده";
            }
        }

        private void ApplyProfileToUi(ProfileResponse profile, string username)
        {
            TxtDisplayName.Text = username;

            bool isExpired = IsSubscriptionExpired(profile.PlanExpiresAt);
            if (isExpired)
            {
                TxtPlanExpires.Text = "اشتراک منقضی شده";
                TxtPlanExpires.Foreground = new SolidColorBrush(Color.FromRgb(255, 75, 90));
                BadgePlan.Background = new SolidColorBrush(Color.FromArgb(35, 255, 75, 90));
                BadgePlan.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 75, 90));
                TxtPlanTier.Text = "منقضی";
                TxtPlanTier.Foreground = new SolidColorBrush(Color.FromRgb(255, 75, 90));
            }
            else
            {
                TxtPlanExpires.Text = FormatRemainingDays(profile.PlanExpiresAt);
                TxtPlanExpires.Foreground = BrushAccentRed;
                BadgePlan.Background = BrushAccentRedGlow;
                BadgePlan.BorderBrush = BrushAccentRed;
                TxtPlanTier.Text = string.IsNullOrEmpty(profile.PlanTier) ? "PRO" : profile.PlanTier.ToUpperInvariant();
                TxtPlanTier.Foreground = BrushAccentRed;
            }

            // Ensure connect button reflects current expiration state if disconnected and not currently connecting
            if (!_isConnected && !_isProcessing)
            {
                SetConnectionUiState(false);
            }

            if (!string.IsNullOrEmpty(profile.RegisteredIp))
            {
                _currentRegisteredIp = profile.RegisteredIp;
            }
            UpdateIpBadgeUi(isDetecting: _isDetectingIp);
        }

        #region Login Events

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowLoginError("لطفاً نام کاربری یا ایمیل را وارد نمایید.");
                TxtUsername.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowLoginError("لطفاً رمز عبور را وارد نمایید.");
                TxtPassword.Focus();
                return;
            }

            BtnLogin.IsEnabled = false;
            LoginErrorBorder.Visibility = Visibility.Collapsed;

            var (success, response, error) = await _apiService.LoginAsync(username, password);

            if (success && response != null && !string.IsNullOrEmpty(response.Token))
            {
                // Always remember token and username for persistent session
                _settings.Token = response.Token;
                _settings.Username = response.WebUsername ?? username;
                _settings.RememberMe = true;
                _storageService.SaveSettings(_settings);

                // Fetch Profile
                var (_, profile, _, _) = await _apiService.GetProfileAsync(response.Token);

                _currentProfile = profile ?? new ProfileResponse();
                _settings.CachedProfile = _currentProfile;
                _storageService.SaveSettings(_settings);

                ApplyProfileToUi(_currentProfile, response.WebUsername ?? username);

                ShowDashboard();
            }
            else
            {
                ShowLoginError(error ?? "نام کاربری یا رمز عبور نامعتبر است.");
            }

            BtnLogin.IsEnabled = true;
        }

        private async void BtnGoogleLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnGoogleLogin.IsEnabled = false;
                LoginErrorBorder.Visibility = Visibility.Collapsed;

                var (authUrl, stateKey) = await _apiService.StartGoogleAuthAsync();
                if (string.IsNullOrEmpty(authUrl) || string.IsNullOrEmpty(stateKey))
                {
                    ShowLoginError("خطا در ایجاد نشست احراز هویت با گوگل.");
                    BtnGoogleLogin.IsEnabled = true;
                    return;
                }

                // Open Default System Browser for Google Sign-In
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                ShowLoginError("مرورگر برای انتخاب حساب گوگل باز شد. لطفاً فرآیند ورود را تکمیل نمایید...");
                TxtLoginError.Foreground = System.Windows.Media.Brushes.LightSkyBlue;

                // Poll for completion (up to 2 minutes)
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(2000);
                    var (finished, success, token, username, error) = await _apiService.PollGoogleAuthAsync(stateKey);
                    if (finished)
                    {
                        if (success && !string.IsNullOrEmpty(token))
                        {
                            _settings.Token = token;
                            _settings.Username = username ?? "کاربر گرامی";
                            _settings.RememberMe = true;
                            _storageService.SaveSettings(_settings);

                            var (_, profile, _, _) = await _apiService.GetProfileAsync(token);
                            _currentProfile = profile ?? new ProfileResponse();
                            _settings.CachedProfile = _currentProfile;
                            _storageService.SaveSettings(_settings);

                            ApplyProfileToUi(_currentProfile, username ?? "کاربر گرامی");
                            ShowDashboard();
                            return;
                        }
                        else
                        {
                            ShowLoginError(error ?? "فرآیند ورود با گوگل تکمیل نشد.");
                            TxtLoginError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 153, 170));
                            BtnGoogleLogin.IsEnabled = true;
                            return;
                        }
                    }
                }

                ShowLoginError("زمان احراز هویت با گوگل به پایان رسید.");
                TxtLoginError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 153, 170));
            }
            catch (Exception ex)
            {
                ShowLoginError($"خطا در ورود با گوگل: {ex.Message}");
                TxtLoginError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 153, 170));
            }
            finally
            {
                BtnGoogleLogin.IsEnabled = true;
            }
        }

        private void ShowLoginError(string message)
        {
            TxtLoginError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 153, 170));
            TxtLoginError.Text = message;
            LoginErrorBorder.Visibility = Visibility.Visible;
        }

        #endregion

        #region Dashboard Actions

        public enum ModalType
        {
            Warning,
            Question,
            Error,
            Info
        }

        private Action? _modalPrimaryAction;
        private Action? _modalSecondaryAction;

        private void ShowModal(string title, string message, string primaryText, Action? onPrimary = null, string? secondaryText = null, Action? onSecondary = null, ModalType type = ModalType.Warning)
        {
            TxtModalTitle.Text = title;
            TxtModalMessage.Text = message;
            TxtModalPrimaryBtn.Text = primaryText;

            switch (type)
            {
                case ModalType.Warning:
                    ModalIconBorder.BorderBrush = BrushAccentYellow;
                    ModalIconBorder.Background = BrushAccentYellowGlow;
                    ModalIconShadow.Color = Color.FromRgb(255, 179, 0);
                    ModalIconPath.Fill = BrushAccentYellow;
                    ModalIconPath.Data = Geometry.Parse("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z");
                    break;
                case ModalType.Question:
                    ModalIconBorder.BorderBrush = BrushAccentRed;
                    ModalIconBorder.Background = BrushAccentRedGlow;
                    ModalIconShadow.Color = Color.FromRgb(255, 59, 92);
                    ModalIconPath.Fill = BrushAccentRed;
                    ModalIconPath.Data = Geometry.Parse("M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z");
                    break;
                case ModalType.Error:
                    ModalIconBorder.BorderBrush = BrushAccentRed;
                    ModalIconBorder.Background = BrushAccentRedGlow;
                    ModalIconShadow.Color = Color.FromRgb(255, 59, 92);
                    ModalIconPath.Fill = BrushAccentRed;
                    ModalIconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    break;
                case ModalType.Info:
                    ModalIconBorder.BorderBrush = BrushAccentRed;
                    ModalIconBorder.Background = BrushAccentRedGlow;
                    ModalIconShadow.Color = Color.FromRgb(255, 59, 92);
                    ModalIconPath.Fill = BrushAccentRed;
                    ModalIconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
                    break;
            }

            if (string.IsNullOrEmpty(secondaryText))
            {
                BtnModalSecondary.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(BtnModalPrimary, 3);
                Grid.SetColumn(BtnModalPrimary, 0);
            }
            else
            {
                BtnModalSecondary.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(BtnModalPrimary, 1);
                Grid.SetColumn(BtnModalPrimary, 2);
                TxtModalSecondaryBtn.Text = secondaryText;
            }

            _modalPrimaryAction = onPrimary;
            _modalSecondaryAction = onSecondary;

            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void HideModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            _modalPrimaryAction = null;
            _modalSecondaryAction = null;
        }

        private void BtnModalPrimary_Click(object sender, RoutedEventArgs e)
        {
            var action = _modalPrimaryAction;
            HideModal();
            action?.Invoke();
        }

        private void BtnModalSecondary_Click(object sender, RoutedEventArgs e)
        {
            var action = _modalSecondaryAction;
            HideModal();
            action?.Invoke();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            // Block connecting if subscription has expired with in-app Persian modal
            if (!_isConnected && IsSubscriptionExpired(_currentProfile?.PlanExpiresAt))
            {
                ShowModal(
                    title: "پایان اعتبار اشتراک",
                    message: "مهلت اشتراک حساب کاربری شما به پایان رسیده است.\nبرای اتصال و استفاده از دی‌ان‌اس ضدتحریم Ryzer، لطفاً نسبت به تمدید اشتراک در وب‌سایت اقدام فرمایید.",
                    primaryText: "تمدید در وب‌سایت",
                    onPrimary: () =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://ryzerdns.ir",
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    },
                    secondaryText: "بستن",
                    onSecondary: null,
                    type: ModalType.Warning);
                return;
            }

            _isProcessing = true;

            try
            {
                if (!_isConnected)
                {
                    // === CONNECT FLOW ===
                    SetConnectingUiState();

                    // Run IP firewall registration and local adapter DNS set concurrently
                    var registerTask = !string.IsNullOrEmpty(_settings.Token)
                        ? _apiService.RegisterIpAsync(_settings.Token, _currentDetectedIp)
                        : Task.FromResult((true, "", (string?)null));

                    var setDnsTask = _dnsService.SetRyzerDnsAsync();
                    var minDisplayTask = Task.Delay(800);

                    await Task.WhenAll(registerTask, setDnsTask, minDisplayTask);
                    var (dnsSuccess, dnsMsg) = await setDnsTask;
                    var (regSuccess, _, regIp) = await registerTask;
                    if (regSuccess && !string.IsNullOrEmpty(regIp))
                    {
                        _currentRegisteredIp = regIp;
                        Dispatcher.Invoke(() => UpdateIpBadgeUi(isDetecting: false));
                    }

                    if (!dnsSuccess)
                    {
                        ShowModal(
                            title: "خطا در تنظیم DNS",
                            message: dnsMsg,
                            primaryText: "متوجه شدم",
                            onPrimary: null,
                            secondaryText: null,
                            type: ModalType.Error);
                        SetConnectionUiState(false);
                        return;
                    }

                    // Background non-blocking profile and ping refresh
                    _ = UpdatePingAsync();
                    if (!string.IsNullOrEmpty(_settings.Token))
                    {
                        _ = Task.Run(async () =>
                        {
                            var (_, prof, _, _) = await _apiService.GetProfileAsync(_settings.Token);
                            if (prof != null)
                            {
                                _currentProfile = prof;
                                _settings.CachedProfile = prof;
                                _storageService.SaveSettings(_settings);
                                Dispatcher.Invoke(() =>
                                {
                                    ApplyProfileToUi(prof, _settings.Username ?? "کاربر");
                                });
                            }
                        });
                    }

                    SetConnectionUiState(true);
                    PlayConnectSound();
                }
                else
                {
                    // === DISCONNECT FLOW ===
                    await _dnsService.ResetDnsToDhcpAsync();
                    SetConnectionUiState(false);
                    _ = UpdatePingAsync();
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async void BtnRefreshPing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = (Path?)BtnRefreshPing.Template.FindName("RefreshPath", BtnRefreshPing);
                if (path?.RenderTransform is RotateTransform rotate)
                {
                    var anim = new DoubleAnimation
                    {
                        From = 0,
                        To = 360,
                        Duration = TimeSpan.FromMilliseconds(450)
                    };
                    rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
                }
            }
            catch
            {
            }

            await UpdatePingAsync();
        }

        #region IP Management & Auto-Check (1-Minute Periodic)

        private async void BtnRegisterIp_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteRegisterIpAsync(silent: false);
        }

        private async Task DetectAndSyncIpAsync(bool silent = false)
        {
            if (_isDetectingIp || _isRegisteringIp) return;
            _isDetectingIp = true;

            if (!silent)
            {
                UpdateIpBadgeUi(isDetecting: true);
            }

            try
            {
                string? detected = await _apiService.DetectIpAsync();
                _isDetectingIp = false;

                if (!string.IsNullOrEmpty(detected))
                {
                    _currentDetectedIp = detected;
                }

                string saved = _currentRegisteredIp ?? _currentProfile?.RegisteredIp ?? "";
                bool isSynced = !string.IsNullOrEmpty(_currentDetectedIp) && _currentDetectedIp == saved;

                if (!string.IsNullOrEmpty(_settings.Token))
                {
                    bool hasChanged = !string.IsNullOrEmpty(_currentDetectedIp) && _currentDetectedIp != saved;
                    if (hasChanged || string.IsNullOrEmpty(saved))
                    {
                        // Auto-register on IP change or initial empty
                        await ExecuteRegisterIpAsync(silent: true);
                        return;
                    }
                }

                UpdateIpBadgeUi(isDetecting: false);
            }
            catch
            {
                _isDetectingIp = false;
                UpdateIpBadgeUi(isDetecting: false);
            }
        }

        private async Task ExecuteRegisterIpAsync(bool silent = false)
        {
            if (string.IsNullOrEmpty(_settings.Token))
            {
                ShowLogin();
                return;
            }

            if (_isRegisteringIp) return;
            _isRegisteringIp = true;

            Dispatcher.Invoke(() =>
            {
                BtnRegisterIp.IsEnabled = false;
                BtnRegisterIp.Content = "در حال ثبت...";
            });

            try
            {
                var (success, msg, regIp) = await _apiService.RegisterIpAsync(_settings.Token, _currentDetectedIp);
                _isRegisteringIp = false;

                if (success)
                {
                    _currentRegisteredIp = !string.IsNullOrEmpty(regIp) ? regIp : _currentDetectedIp;
                    if (_currentProfile != null)
                    {
                        _currentProfile.RegisteredIp = _currentRegisteredIp;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        BtnRegisterIp.IsEnabled = true;
                        UpdateIpBadgeUi(isDetecting: false);
                        if (!silent)
                        {
                            ShowModal(
                                title: "ثبت موفق آی‌پی",
                                message: $"آدرس آی‌پی سیستم شما ({_currentRegisteredIp}) با موفقیت در سیستم RyzerDNS ثبت و همگام شد.",
                                primaryText: "بسیار عالی",
                                onPrimary: null,
                                type: ModalType.Info);
                        }
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        BtnRegisterIp.IsEnabled = true;
                        UpdateIpBadgeUi(isDetecting: false);
                        if (!silent)
                        {
                            ShowModal(
                                title: "خطا در ثبت آی‌پی",
                                message: msg,
                                primaryText: "تایید",
                                onPrimary: null,
                                type: ModalType.Warning);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _isRegisteringIp = false;
                Dispatcher.Invoke(() =>
                {
                    BtnRegisterIp.IsEnabled = true;
                    UpdateIpBadgeUi(isDetecting: false);
                    if (!silent)
                    {
                        ShowModal(
                            title: "خطا در اتصال",
                            message: $"خطا در اتصال به سرور: {ex.Message}",
                            primaryText: "تایید",
                            onPrimary: null,
                            type: ModalType.Error);
                    }
                });
            }
        }

        private void UpdateIpBadgeUi(bool isDetecting = false)
        {
            Dispatcher.Invoke(() =>
            {
                bool hasDetected = !string.IsNullOrEmpty(_currentDetectedIp);
                string registeredIp = _currentRegisteredIp ?? _currentProfile?.RegisteredIp ?? "";
                bool isSynced = !string.IsNullOrEmpty(_settings.Token) &&
                                !string.IsNullOrEmpty(registeredIp) &&
                                hasDetected &&
                                registeredIp == _currentDetectedIp;

                // 1. IP Text Box
                if (hasDetected)
                {
                    TxtDetectedIp.Text = _currentDetectedIp;
                    TxtDetectedIp.Foreground = BrushBadgeBlueText;
                }
                else if (!string.IsNullOrEmpty(registeredIp))
                {
                    TxtDetectedIp.Text = registeredIp;
                    TxtDetectedIp.Foreground = Brushes.White;
                }
                else if (isDetecting)
                {
                    TxtDetectedIp.Text = "در حال دریافت...";
                    TxtDetectedIp.Foreground = BrushTextSecondary;
                }
                else
                {
                    TxtDetectedIp.Text = "شناسایی‌نشده";
                    TxtDetectedIp.Foreground = BrushTextMuted;
                }

                // 2. Status Badge Pill
                if (isDetecting)
                {
                    TxtIpStatus.Text = "در حال بررسی";
                    TxtIpStatus.Foreground = BrushBadgeBlueText;
                    DotIpStatus.Fill = BrushBadgeBlueText;
                    BadgeIpStatus.Background = BrushBadgeBlueBg;
                    BadgeIpStatus.BorderBrush = BrushBadgeBlueBorder;
                }
                else if (isSynced)
                {
                    TxtIpStatus.Text = "همگام و ثبت‌شده";
                    TxtIpStatus.Foreground = BrushBadgeGreenText;
                    DotIpStatus.Fill = BrushBadgeGreenText;
                    BadgeIpStatus.Background = BrushBadgeGreenBg;
                    BadgeIpStatus.BorderBrush = BrushBadgeGreenBorder;
                }
                else if (!string.IsNullOrEmpty(_settings.Token))
                {
                    if (hasDetected)
                    {
                        TxtIpStatus.Text = "نیاز به ثبت";
                        TxtIpStatus.Foreground = BrushBadgeOrangeText;
                        DotIpStatus.Fill = BrushBadgeOrangeText;
                        BadgeIpStatus.Background = BrushBadgeOrangeBg;
                        BadgeIpStatus.BorderBrush = BrushBadgeOrangeBorder;
                    }
                    else
                    {
                        TxtIpStatus.Text = "نامشخص";
                        TxtIpStatus.Foreground = BrushBadgeGrayText;
                        DotIpStatus.Fill = BrushBadgeGrayText;
                        BadgeIpStatus.Background = BrushBadgeGrayBg;
                        BadgeIpStatus.BorderBrush = BrushBadgeGrayBorder;
                    }
                }
                else
                {
                    TxtIpStatus.Text = hasDetected ? "نیاز به ورود" : "نامشخص";
                    TxtIpStatus.Foreground = hasDetected ? BrushBadgeOrangeText : BrushBadgeGrayText;
                    DotIpStatus.Fill = hasDetected ? BrushBadgeOrangeText : BrushBadgeGrayText;
                    BadgeIpStatus.Background = hasDetected ? BrushBadgeOrangeBg : BrushBadgeGrayBg;
                    BadgeIpStatus.BorderBrush = hasDetected ? BrushBadgeOrangeBorder : BrushBadgeGrayBorder;
                }

                // 3. Register Button text
                if (isSynced)
                {
                    BtnRegisterIp.Content = "ثبت مجدد";
                }
                else
                {
                    BtnRegisterIp.Content = "ثبت آی‌پی";
                }
            });
        }

        #endregion

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            ShowModal(
                title: "خروج از حساب کاربری",
                message: "آیا مطمئن هستید که می‌خواهید از حساب خود خارج شوید؟\nدر صورت خروج، اتصال دی‌ان‌اس به طور خودکار خاموش خواهد شد.",
                primaryText: "خروج از حساب",
                onPrimary: async () =>
                {
                    // Automatically turn off DNS if active before logging out
                    if (_isConnected || _dnsService.IsRyzerDnsActive())
                    {
                        await _dnsService.ResetDnsToDhcpAsync();
                        _isConnected = false;
                        SetConnectionUiState(false);
                    }

                    _storageService.ClearSession();
                    _settings.Token = null;
                    ShowLogin();
                    TxtPassword.Clear();
                },
                secondaryText: "انصراف",
                onSecondary: null,
                type: ModalType.Question);
        }

        #endregion

        #region UI State Helpers

        private void SetConnectingUiState()
        {
            var ambientGlow = (Ellipse?)BtnConnect.Template.FindName("AmbientGlow", BtnConnect);
            var orbitSpinner = (Ellipse?)BtnConnect.Template.FindName("OrbitSpinner", BtnConnect);
            var outerRim = (Border?)BtnConnect.Template.FindName("OuterRim", BtnConnect);
            var innerDisc = (Border?)BtnConnect.Template.FindName("InnerDisc", BtnConnect);
            var powerIcon = (Path?)BtnConnect.Template.FindName("PowerIcon", BtnConnect);
            var iconGlow = (DropShadowEffect?)BtnConnect.Template.FindName("IconGlow", BtnConnect);
            var txtBtnState = (TextBlock?)BtnConnect.Template.FindName("TxtBtnState", BtnConnect);

            // 1. Ambient Glow -> Amber Diffusion
            if (ambientGlow != null)
            {
                ambientGlow.Fill = new SolidColorBrush(Color.FromArgb(50, 255, 179, 0));
                if (ambientGlow.Effect is DropShadowEffect glow)
                {
                    glow.Color = Color.FromRgb(255, 179, 0);
                    glow.Opacity = 0.55;
                    glow.BlurRadius = 45;
                }
            }

            // 2. Silky Smooth Laser Arc (Responsive, fresh 1.1s orbit rotation)
            if (orbitSpinner != null)
            {
                orbitSpinner.Visibility = Visibility.Visible;
                var rotate = new RotateTransform(0);
                orbitSpinner.RenderTransformOrigin = new Point(0.5, 0.5);
                orbitSpinner.RenderTransform = rotate;

                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromMilliseconds(1100),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
            }

            // 3. Outer Bezel Rim Ring -> Amber Hairline
            if (outerRim != null)
            {
                outerRim.BorderBrush = new LinearGradientBrush(
                    Color.FromRgb(140, 90, 15),
                    Color.FromRgb(40, 25, 5),
                    new Point(0, 0),
                    new Point(1, 1));
                outerRim.Background = new SolidColorBrush(Color.FromRgb(14, 10, 6));
            }

            // 4. Main Glass Switch Disc -> Deep Amber Glass
            if (innerDisc != null)
            {
                innerDisc.BorderBrush = new LinearGradientBrush(
                    Color.FromRgb(180, 120, 20),
                    Color.FromRgb(50, 30, 8),
                    new Point(0, 0),
                    new Point(0, 1));
                innerDisc.Background = new LinearGradientBrush(
                    Color.FromRgb(36, 24, 10),
                    Color.FromRgb(16, 10, 4),
                    new Point(0, 0),
                    new Point(0, 1));
            }

            // 5. Center Power Vector
            if (powerIcon != null)
            {
                powerIcon.Fill = BrushAccentYellow;
                powerIcon.Data = Geometry.Parse("M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42C17.99 7.86 19 9.81 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.19 1.01-4.14 2.58-5.42L6.17 5.17C4.23 6.82 3 9.26 3 12c0 4.97 4.03 9 9 9s9-4.03 9-9c0-2.74-1.23-5.18-3.17-6.83z");
            }

            if (iconGlow != null)
            {
                iconGlow.Color = Color.FromRgb(255, 179, 0);
                iconGlow.Opacity = 0.7;
            }

            // 6. State Label
            if (txtBtnState != null)
            {
                txtBtnState.Text = "در حال اتصال ...\u200F";
                txtBtnState.Foreground = BrushAccentYellow;
            }
        }

        private void SetConnectionUiState(bool connected)
        {
            _isConnected = connected;

            // Update Windows Taskbar overlay badge
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (TaskbarItemInfo == null)
                    {
                        TaskbarItemInfo = new TaskbarItemInfo();
                    }
                    TaskbarItemInfo.Overlay = connected ? ConnectedOverlayIcon : null;
                    TaskbarItemInfo.Description = connected ? "RyzerDNS - متصل" : "RyzerDNS Client";
                });
            }
            catch
            {
            }

            var ambientGlow = (Ellipse?)BtnConnect.Template.FindName("AmbientGlow", BtnConnect);
            var orbitSpinner = (Ellipse?)BtnConnect.Template.FindName("OrbitSpinner", BtnConnect);
            var outerRim = (Border?)BtnConnect.Template.FindName("OuterRim", BtnConnect);
            var innerDisc = (Border?)BtnConnect.Template.FindName("InnerDisc", BtnConnect);
            var powerIcon = (Path?)BtnConnect.Template.FindName("PowerIcon", BtnConnect);
            var iconGlow = (DropShadowEffect?)BtnConnect.Template.FindName("IconGlow", BtnConnect);
            var txtBtnState = (TextBlock?)BtnConnect.Template.FindName("TxtBtnState", BtnConnect);

            // Stop and hide orbit spinner
            if (orbitSpinner != null)
            {
                orbitSpinner.Visibility = Visibility.Collapsed;
                if (orbitSpinner.RenderTransform is RotateTransform rotate)
                {
                    rotate.BeginAnimation(RotateTransform.AngleProperty, null);
                }
            }

            if (connected)
            {
                // Connected State: Emerald Glass Aurora
                if (ambientGlow != null)
                {
                    ambientGlow.Fill = new SolidColorBrush(Color.FromArgb(60, 0, 230, 118));
                    if (ambientGlow.Effect is DropShadowEffect glow)
                    {
                        glow.Color = Color.FromRgb(0, 230, 118);
                        glow.Opacity = 0.55;
                        glow.BlurRadius = 50;
                    }
                }

                if (outerRim != null)
                {
                    outerRim.BorderBrush = new LinearGradientBrush(
                        Color.FromRgb(0, 200, 100),
                        Color.FromRgb(0, 60, 30),
                        new Point(0, 0),
                        new Point(1, 1));
                    outerRim.Background = new SolidColorBrush(Color.FromRgb(4, 18, 10));
                }

                if (innerDisc != null)
                {
                    innerDisc.BorderBrush = new LinearGradientBrush(
                        Color.FromRgb(0, 230, 118),
                        Color.FromRgb(0, 80, 40),
                        new Point(0, 0),
                        new Point(0, 1));
                    innerDisc.Background = new LinearGradientBrush(
                        Color.FromRgb(10, 46, 24),
                        Color.FromRgb(4, 20, 10),
                        new Point(0, 0),
                        new Point(0, 1));
                }

                if (powerIcon != null)
                {
                    powerIcon.Fill = Brushes.White;
                    powerIcon.Data = Geometry.Parse("M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42C17.99 7.86 19 9.81 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.19 1.01-4.14 2.58-5.42L6.17 5.17C4.23 6.82 3 9.26 3 12c0 4.97 4.03 9 9 9s9-4.03 9-9c0-2.74-1.23-5.18-3.17-6.83z");
                }

                if (iconGlow != null)
                {
                    iconGlow.Color = Color.FromRgb(0, 230, 118);
                    iconGlow.Opacity = 0.8;
                }

                if (txtBtnState != null)
                {
                    txtBtnState.Text = _remoteConfig?.UiConfig?.DisconnectBtnText ?? "قطع اتصال";
                    txtBtnState.Foreground = Brushes.White;
                }
            }
            else
            {
                bool isExpired = IsSubscriptionExpired(_currentProfile?.PlanExpiresAt);

                if (isExpired)
                {
                    // Expired State: Sleek Slate Glass with Lock
                    if (ambientGlow != null)
                    {
                        ambientGlow.Fill = Brushes.Transparent;
                        if (ambientGlow.Effect is DropShadowEffect glow)
                        {
                            glow.Opacity = 0.0;
                        }
                    }

                    if (outerRim != null)
                    {
                        outerRim.BorderBrush = new SolidColorBrush(Color.FromRgb(36, 36, 50));
                        outerRim.Background = new SolidColorBrush(Color.FromRgb(10, 10, 16));
                    }

                    if (innerDisc != null)
                    {
                        innerDisc.BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 64));
                        innerDisc.Background = new LinearGradientBrush(
                            Color.FromRgb(18, 18, 26),
                            Color.FromRgb(10, 10, 14),
                            new Point(0, 0),
                            new Point(0, 1));
                    }

                    if (powerIcon != null)
                    {
                        powerIcon.Fill = BrushTextSecondary;
                        powerIcon.Data = Geometry.Parse("M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z");
                    }

                    if (iconGlow != null)
                    {
                        iconGlow.Opacity = 0.0;
                    }

                    if (txtBtnState != null)
                    {
                        txtBtnState.Text = "اشتراک منقضی";
                        txtBtnState.Foreground = BrushTextSecondary;
                    }
                }
                else
                {
                    // Ready Disconnected State: Deep Crimson Cyber-Glass
                    if (ambientGlow != null)
                    {
                        ambientGlow.Fill = new SolidColorBrush(Color.FromArgb(45, 255, 59, 92));
                        if (ambientGlow.Effect is DropShadowEffect glow)
                        {
                            glow.Color = Color.FromRgb(255, 59, 92);
                            glow.Opacity = 0.45;
                            glow.BlurRadius = 45;
                        }
                    }

                    if (outerRim != null)
                    {
                        outerRim.BorderBrush = new LinearGradientBrush(
                            Color.FromRgb(110, 26, 44),
                            Color.FromRgb(30, 8, 14),
                            new Point(0, 0),
                            new Point(1, 1));
                        outerRim.Background = new SolidColorBrush(Color.FromRgb(12, 4, 8));
                    }

                    if (innerDisc != null)
                    {
                        innerDisc.BorderBrush = new LinearGradientBrush(
                            Color.FromRgb(140, 30, 52),
                            Color.FromRgb(40, 8, 16),
                            new Point(0, 0),
                            new Point(0, 1));
                        innerDisc.Background = new LinearGradientBrush(
                            Color.FromRgb(36, 11, 19),
                            Color.FromRgb(16, 4, 8),
                            new Point(0, 0),
                            new Point(0, 1));
                    }

                    if (powerIcon != null)
                    {
                        powerIcon.Fill = Brushes.White;
                        powerIcon.Data = Geometry.Parse("M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42C17.99 7.86 19 9.81 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.19 1.01-4.14 2.58-5.42L6.17 5.17C4.23 6.82 3 9.26 3 12c0 4.97 4.03 9 9 9s9-4.03 9-9c0-2.74-1.23-5.18-3.17-6.83z");
                    }

                    if (iconGlow != null)
                    {
                        iconGlow.Color = Color.FromRgb(255, 59, 92);
                        iconGlow.Opacity = 0.6;
                    }

                    if (txtBtnState != null)
                    {
                        txtBtnState.Text = _remoteConfig?.UiConfig?.ConnectBtnText ?? "اتصال";
                        txtBtnState.Foreground = Brushes.White;
                    }
                }
            }
        }

        #endregion

        #region Audio Feedback

        private static byte[]? _cachedConnectSound;

        private static void PlayConnectSound()
        {
            Task.Run(() =>
            {
                try
                {
                    _cachedConnectSound ??= GenerateMinimalChime();
                    using var ms = new MemoryStream(_cachedConnectSound);
                    using var sp = new SoundPlayer(ms);
                    sp.PlaySync();
                }
                catch
                {
                    try { SystemSounds.Asterisk.Play(); } catch { }
                }
            });
        }

        private static byte[] GenerateMinimalChime()
        {
            int sampleRate = 44100;
            // Two gentle, glassy tones: D5 (587.33 Hz) and A5 (880.00 Hz) - minimalist uplifting chime
            double note1Freq = 587.33;
            double note2Freq = 880.00;
            int note1Samples = (int)(sampleRate * 0.08); // 80 ms
            int note2Samples = (int)(sampleRate * 0.20); // 200 ms
            int totalSamples = note1Samples + note2Samples;

            short[] samples = new short[totalSamples];
            double maxAmp = 7500.0; // Gentle, silky volume (peak is ~23% of 32767)

            // Note 1: smooth sine with attack and decay
            for (int i = 0; i < note1Samples; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = Math.Sin(Math.PI * i / note1Samples);
                double wave = Math.Sin(2 * Math.PI * note1Freq * t);
                samples[i] = (short)(wave * envelope * (maxAmp * 0.7));
            }

            // Note 2: high note with exponential decay for a clean glassy ring
            for (int i = 0; i < note2Samples; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = Math.Exp(-5.5 * i / note2Samples) * Math.Sin(Math.Min(1.0, 12.0 * i / note2Samples) * Math.PI / 2);
                double wave = Math.Sin(2 * Math.PI * note2Freq * t) + 0.12 * Math.Sin(2 * Math.PI * (note2Freq * 2) * t);
                samples[note1Samples + i] = (short)(wave * envelope * maxAmp);
            }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write((int)(36 + totalSamples * 2));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write((int)16);           // PCM chunk size
            bw.Write((short)1);          // Format: PCM
            bw.Write((short)1);          // Mono
            bw.Write((int)sampleRate);   // 44100
            bw.Write((int)(sampleRate * 2));
            bw.Write((short)2);          // Block align
            bw.Write((short)16);         // 16-bit
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write((int)(totalSamples * 2));

            for (int i = 0; i < totalSamples; i++)
            {
                bw.Write(samples[i]);
            }

            return ms.ToArray();
        }

        #endregion

        #region Window Chrome & Exit Events

        private bool _isExitingConfirmed = false;

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isExitingConfirmed)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            PromptExit();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            PromptExit();
        }

        private void PromptExit()
        {
            if (_isExitingConfirmed) return;

            bool dnsActive = _isConnected || _dnsService.IsRyzerDnsActive();

            if (dnsActive)
            {
                ShowModal(
                    title: "تایید خروج از برنامه",
                    message: "اتصال دی‌ان‌اس در سیستم شما فعال است.\nبا خروج، اتصال قطع شده و تنظیمات شبکه ویندوز به صورت خودکار به حالت اولیه بازمی‌گردد.\n\nآیا برای بستن برنامه اطمینان دارید؟",
                    primaryText: "خروج و قطع اتصال",
                    onPrimary: async () =>
                    {
                        _isExitingConfirmed = true;
                        try
                        {
                            await _dnsService.ResetDnsToDhcpAsync();
                        }
                        catch { }
                        Application.Current.Shutdown();
                    },
                    secondaryText: "انصراف",
                    onSecondary: null,
                    type: ModalType.Warning);
            }
            else
            {
                ShowModal(
                    title: "خروج از برنامه",
                    message: "آیا برای بستن و خروج از برنامه اطمینان دارید؟",
                    primaryText: "خروج از برنامه",
                    onPrimary: async () =>
                    {
                        _isExitingConfirmed = true;
                        try
                        {
                            await _dnsService.ResetDnsToDhcpAsync();
                        }
                        catch { }
                        Application.Current.Shutdown();
                    },
                    secondaryText: "انصراف",
                    onSecondary: null,
                    type: ModalType.Question);
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowserUrl("https://ryzerdns.ir");
        }

        private void BtnOpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            string url = _remoteConfig?.UiConfig?.WebsiteUrl ?? "https://ryzerdns.ir";
            OpenBrowserUrl(url);
        }

        private void BtnOpenTelegram_Click(object sender, RoutedEventArgs e)
        {
            string url = _remoteConfig?.UiConfig?.TelegramChannelUrl ?? "https://t.me/ryzerdns";
            OpenBrowserUrl(url);
        }

        private void BtnOpenBot_Click(object sender, RoutedEventArgs e)
        {
            string url = _remoteConfig?.UiConfig?.TelegramBotUrl ?? "https://t.me/RyzerDnsBot";
            OpenBrowserUrl(url);
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            string title = _remoteConfig?.UiConfig?.AboutTitle ?? "درباره رایزر دی‌ان‌اس";
            string message = _remoteConfig?.UiConfig?.AboutMessage ?? "رایزر دی‌ان‌اس | RyzerDNS Client\n\n• سرویس هوشمند کاهش پینگ و رفع تحریم بازی‌ها\n• بهینه‌سازی اختصاصی استیم، اپیک‌گیمز، بتل‌نت، هوش مصنوعی و دیسکورد\n• پایداری حداکثری و ترافیک Clean Direct\n\nوب‌سایت: ryzerdns.ir\nکانال تلگرام: @RyzerDNS";
            string buttonText = _remoteConfig?.UiConfig?.AboutButtonText ?? "متوجه شدم";

            ShowModal(title, message, buttonText, null, null, null, ModalType.Info);
        }

        private static void OpenBrowserUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        #endregion

        #region Taskbar Icon Overlay

        private static ImageSource? CreateConnectedOverlayIcon()
        {
            try
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    var darkBrush = new SolidColorBrush(Color.FromArgb(255, 10, 14, 22));
                    var greenBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                    var pen = new Pen(Brushes.White, 3.2)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Round
                    };

                    // Dark outline circle for high contrast against light or dark Windows taskbars
                    dc.DrawEllipse(darkBrush, null, new Point(16, 16), 15.5, 15.5);
                    // Vibrant emerald green badge
                    dc.DrawEllipse(greenBrush, null, new Point(16, 16), 13.0, 13.0);

                    // Crisp centered white checkmark
                    var pathGeo = new PathGeometry();
                    var figure = new PathFigure { StartPoint = new Point(10.5, 16.5), IsFilled = false };
                    figure.Segments.Add(new LineSegment(new Point(14.5, 20.5), true));
                    figure.Segments.Add(new LineSegment(new Point(22.5, 11.5), true));
                    pathGeo.Figures.Add(figure);

                    dc.DrawGeometry(null, pen, pathGeo);
                }

                var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                return rtb;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
