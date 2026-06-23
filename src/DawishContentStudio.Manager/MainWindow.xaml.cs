using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DawishContentStudio.Core;
using Microsoft.Win32;
using WpfControls = System.Windows.Controls;

namespace DawishContentStudio.Manager;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<LocalImageItem> _images = new();
    private readonly AppSettingsStore _settingsStore = new();
    private AppSettings _settings = new();
    private CancellationTokenSource? _shopCts;

    public MainWindow()
    {
        InitializeComponent();
        ImagesListBox.ItemsSource = _images;
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        FillTimeCombos();
        _settings = _settingsStore.Load();
        ApplySettingsToUi();
        MainTabs.SelectedIndex = string.IsNullOrWhiteSpace(_settings.WorkerUrl) || string.IsNullOrWhiteSpace(_settings.Token) ? 0 : 1;
        UpdateRoleBadge();
        UpdatePreview();
    }

    private void FillTimeCombos()
    {
        for (var i = 1; i <= 12; i++) HourComboBox.Items.Add(i.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < 60; i += 5) MinuteComboBox.Items.Add(i.ToString("00", CultureInfo.InvariantCulture));
        HourComboBox.SelectedItem = "7";
        MinuteComboBox.SelectedItem = "00";
    }

    private void ApplySettingsToUi()
    {
        WorkerUrlTextBox.Text = _settings.WorkerUrl;
        SettingsWorkerUrlTextBox.Text = _settings.WorkerUrl;
        TokenPasswordBox.Password = _settings.Token;
        SettingsTokenPasswordBox.Password = _settings.Token;
        RoleComboBox.SelectedIndex = _settings.Role == DeviceRole.Shop ? 1 : 0;
    }

    private CloudflareClient Client()
    {
        var url = (SettingsWorkerUrlTextBox.Text.Trim().Length > 0 ? SettingsWorkerUrlTextBox.Text.Trim() : WorkerUrlTextBox.Text.Trim()).TrimEnd('/');
        var token = SettingsTokenPasswordBox.Password.Trim().Length > 0 ? SettingsTokenPasswordBox.Password.Trim() : TokenPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("ضع Worker URL أولًا.");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("ضع الرمز أولًا.");
        return new CloudflareClient(url, token);
    }

    private void SaveSettingsInternal(DeviceRole? role = null)
    {
        var currentRole = role ?? ((RoleComboBox.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString() == "shop" ? DeviceRole.Shop : DeviceRole.Admin);
        _settings = new AppSettings
        {
            WorkerUrl = WorkerUrlTextBox.Text.Trim().Length > 0 ? WorkerUrlTextBox.Text.Trim() : SettingsWorkerUrlTextBox.Text.Trim(),
            Token = TokenPasswordBox.Password.Trim().Length > 0 ? TokenPasswordBox.Password.Trim() : SettingsTokenPasswordBox.Password.Trim(),
            Role = currentRole
        };
        _settingsStore.Save(_settings);
        ApplySettingsToUi();
        UpdateRoleBadge();
    }

    private void UpdateRoleBadge()
    {
        RoleBadgeText.Text = _settings.Role == DeviceRole.Shop ? "الوضع: جهاز المحل" : "الوضع: أنا المدير";
        NavPosts.IsEnabled = _settings.Role != DeviceRole.Shop;
        NavQueue.IsEnabled = true;
    }

    private void ShowStart_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 0;
    private void ShowDashboard_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 1;
    private void ShowPosts_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;
    private void ShowShop_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 3;
    private void ShowSettings_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 4;

    private void SetAdminRole_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsInternal(DeviceRole.Admin);
        System.Windows.MessageBox.Show("تم اختيار هذا الجهاز كمدير. ارفع الصور وجدولها من صفحة رفع الصور.");
        MainTabs.SelectedIndex = 2;
    }

    private void SetShopRole_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsInternal(DeviceRole.Shop);
        System.Windows.MessageBox.Show("تم اختيار هذا الجهاز كجهاز المحل. شغّل محطة المحل من صفحة جهاز المحل.");
        MainTabs.SelectedIndex = 3;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsInternal();
        System.Windows.MessageBox.Show("تم حفظ الإعدادات.");
    }

    private void SaveSettingsFromSettingsTab_Click(object sender, RoutedEventArgs e)
    {
        WorkerUrlTextBox.Text = SettingsWorkerUrlTextBox.Text.Trim();
        TokenPasswordBox.Password = SettingsTokenPasswordBox.Password.Trim();
        SaveSettingsInternal();
        System.Windows.MessageBox.Show("تم حفظ الإعدادات.");
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettingsInternal();
            var result = await Client().HealthAsync();
            ConnectionStatusText.Text = "Cloudflare: متصل";
            CloudStatusCardText.Text = "متصل";
            System.Windows.MessageBox.Show(result.Message, "فحص الاتصال");
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "Cloudflare: مشكلة";
            CloudStatusCardText.Text = "مشكلة";
            System.Windows.MessageBox.Show(ex.Message, "فشل الاتصال");
        }
    }

    private void ChooseImages_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        foreach (var file in dialog.FileNames)
        {
            if (_images.Any(i => string.Equals(i.Path, file, StringComparison.OrdinalIgnoreCase))) continue;
            _images.Add(new LocalImageItem { Path = file, FileName = Path.GetFileName(file) });
        }
        if (_images.Count > 0 && ImagesListBox.SelectedIndex < 0) ImagesListBox.SelectedIndex = 0;
        UpdatePreview();
    }

    private void ImagesListBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        LoadSelectedImage();
        UpdatePreview();
    }

    private void LoadSelectedImage()
    {
        if (ImagesListBox.SelectedItem is not LocalImageItem item || !File.Exists(item.Path))
        {
            SelectedImagePreview.Source = null;
            PhoneImagePreview.Source = null;
            EmptyImageText.Visibility = Visibility.Visible;
            return;
        }
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(item.Path);
            bitmap.EndInit();
            SelectedImagePreview.Source = bitmap;
            PhoneImagePreview.Source = bitmap;
            EmptyImageText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            SelectedImagePreview.Source = null;
            PhoneImagePreview.Source = null;
            EmptyImageText.Visibility = Visibility.Visible;
        }
    }

    private void PostFields_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        PreviewCaptionText.Text = CaptionTextBox.Text;
        var locations = new List<string>();
        if (InstagramCheckBox.IsChecked == true) locations.Add("Instagram: " + InstagramLocationTextBox.Text);
        if (TikTokCheckBox.IsChecked == true) locations.Add("TikTok: " + TikTokLocationTextBox.Text);
        if (SnapchatCheckBox.IsChecked == true) locations.Add("Snapchat: " + SnapchatLocationTextBox.Text);
        PreviewLocationText.Text = string.Join("\n", locations.Where(x => !string.IsNullOrWhiteSpace(x)));
        PreviewPlatformText.Text = string.Join(" / ", new[]
        {
            InstagramCheckBox.IsChecked == true ? "Instagram" : null,
            TikTokCheckBox.IsChecked == true ? "TikTok" : null,
            SnapchatCheckBox.IsChecked == true ? "Snapchat" : null
        }.Where(x => x is not null));
    }

    private DateTime BuildScheduledAt(int offsetMinutes = 0)
    {
        var date = ScheduleDatePicker.SelectedDate ?? DateTime.Today;
        var hour = int.TryParse(HourComboBox.SelectedItem?.ToString(), out var h) ? h : 7;
        var minute = int.TryParse(MinuteComboBox.SelectedItem?.ToString(), out var m) ? m : 0;
        var isPm = (AmPmComboBox.SelectedItem as WpfControls.ComboBoxItem)?.Content?.ToString() == "م";
        if (hour == 12) hour = 0;
        if (isPm) hour += 12;
        return date.Date.AddHours(hour).AddMinutes(minute).AddMinutes(offsetMinutes);
    }

    private PostUploadRequest BuildUploadRequest(DateTime scheduledAt)
    {
        var caption = CaptionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(caption)) throw new InvalidOperationException("اكتب الكابشن الأساسي أولًا.");
        var guard = new MedicalClaimsGuard();
        var violations = guard.FindViolations(caption);
        if (violations.Count > 0) throw new InvalidOperationException("الكابشن يحتوي كلام طبي ممنوع: " + string.Join(", ", violations));
        return new PostUploadRequest
        {
            Caption = caption,
            InstagramEnabled = InstagramCheckBox.IsChecked == true,
            TikTokEnabled = TikTokCheckBox.IsChecked == true,
            SnapchatEnabled = SnapchatCheckBox.IsChecked == true,
            InstagramLocation = InstagramLocationTextBox.Text.Trim(),
            TikTokLocation = TikTokLocationTextBox.Text.Trim(),
            SnapchatLocation = SnapchatLocationTextBox.Text.Trim(),
            ScheduledAt = scheduledAt,
            TikTokMode = "image_only",
            SnapchatMode = "image_only"
        };
    }

    private async void ScheduleSelectedImage_Click(object sender, RoutedEventArgs e)
    {
        if (ImagesListBox.SelectedItem is not LocalImageItem image)
        {
            System.Windows.MessageBox.Show("اختر صورة أولًا.");
            return;
        }
        await UploadOneAsync(image, BuildScheduledAt(), true);
    }

    private async void ScheduleAllImages_Click(object sender, RoutedEventArgs e)
    {
        if (_images.Count == 0)
        {
            System.Windows.MessageBox.Show("ارفع الصور أولًا.");
            return;
        }
        var spacing = int.TryParse((SpacingMinutesComboBox.SelectedItem as WpfControls.ComboBoxItem)?.Content?.ToString(), out var sp) ? sp : 60;
        var ok = System.Windows.MessageBox.Show($"سيتم رفع وجدولة {_images.Count} صورة. كل صورة منشور مستقل. الفرق بين كل صورة {spacing} دقيقة.", "تأكيد", MessageBoxButton.OKCancel);
        if (ok != MessageBoxResult.OK) return;
        for (var i = 0; i < _images.Count; i++)
        {
            await UploadOneAsync(_images[i], BuildScheduledAt(i * spacing), false);
        }
        UploadStatusText.Text = "تم رفع وجدولة كل الصور.";
    }

    private async Task UploadOneAsync(LocalImageItem image, DateTime scheduledAt, bool showMessage)
    {
        try
        {
            SaveSettingsInternal(DeviceRole.Admin);
            var request = BuildUploadRequest(scheduledAt);
            UploadStatusText.Text = "جاري رفع: " + image.FileName;
            var result = await Client().UploadPostAsync(request, image.Path);
            image.Status = "مجدول";
            image.RemoteId = result.Id;
            ImagesListBox.Items.Refresh();
            UploadStatusText.Text = $"تمت الجدولة: {image.FileName} — {scheduledAt:yyyy-MM-dd hh:mm tt}";
            if (showMessage) System.Windows.MessageBox.Show("تم رفع الصورة وجدولتها في Cloudflare.");
            await RefreshAdminPostsAsync();
        }
        catch (Exception ex)
        {
            UploadStatusText.Text = "فشل: " + ex.Message;
            if (showMessage) System.Windows.MessageBox.Show(ex.Message, "خطأ");
        }
    }

    private void ApplyToAllImages_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("تم اعتماد نفس الكابشن والمنصات واللوكيشن والوقت كبداية لكل الصور. عند الضغط على جدولة كل الصور، كل صورة ستنزل كمنشور مستقل.");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Role == DeviceRole.Shop) await RefreshDuePostsAsync();
        else await RefreshAdminPostsAsync();
    }

    private async Task RefreshAdminPostsAsync()
    {
        try
        {
            var posts = await Client().ListPostsAsync();
            UpcomingCardText.Text = posts.Count(p => p.Status != "posted").ToString(CultureInfo.InvariantCulture);
            CloudStatusCardText.Text = "متصل";
        }
        catch
        {
            CloudStatusCardText.Text = "مشكلة";
        }
    }

    private async void RefreshDuePosts_Click(object sender, RoutedEventArgs e) => await RefreshDuePostsAsync();

    private async Task RefreshDuePostsAsync()
    {
        try
        {
            var posts = await Client().GetDuePostsAsync();
            DuePostsListBox.Items.Clear();
            foreach (var p in posts)
                DuePostsListBox.Items.Add($"{p.ScheduledAt.ToLocalTime():yyyy-MM-dd hh:mm tt} — {p.Caption} — TikTok:{p.TikTokMode}");
            ShopStatusCardText.Text = "متصل";
            ConnectionStatusText.Text = "Cloudflare: متصل";
        }
        catch (Exception ex)
        {
            DuePostsListBox.Items.Clear();
            DuePostsListBox.Items.Add("فشل جلب المنشورات: " + ex.Message);
            ShopStatusCardText.Text = "مشكلة";
        }
    }

    private void StartShopStation_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsInternal(DeviceRole.Shop);
        if (_shopCts is not null)
        {
            System.Windows.MessageBox.Show("محطة المحل تعمل بالفعل.");
            return;
        }
        _shopCts = new CancellationTokenSource();
        ShopStationText.Text = "محطة المحل تعمل. لا تطفئ الجهاز.";
        _ = ShopLoopAsync(_shopCts.Token);
    }


    private async Task ShopLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RunShopCycleAsync();
                await Task.Delay(TimeSpan.FromSeconds(45), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ShopStationText.Text = "محطة المحل: خطأ — " + ex.Message;
                try { await Task.Delay(TimeSpan.FromSeconds(45), token); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private void StopShopStation_Click(object sender, RoutedEventArgs e)
    {
        _shopCts?.Cancel();
        _shopCts = null;
        ShopStationText.Text = "محطة المحل متوقفة";
    }

    private async void RunShopOnce_Click(object sender, RoutedEventArgs e) => await RunShopCycleAsync();

    private async Task RunShopCycleAsync()
    {
        var client = Client();
        await client.HeartbeatAsync(Environment.MachineName, "shop");
        var posts = await client.GetDuePostsAsync();
        DuePostsListBox.Items.Clear();
        foreach (var post in posts)
        {
            DuePostsListBox.Items.Add($"مستحق الآن: {post.Caption}");
            var folder = Path.Combine(_settingsStore.AppFolder, "shop-posts", post.Id);
            Directory.CreateDirectory(folder);
            var imagePath = Path.Combine(folder, "image.jpg");
            await client.DownloadMediaAsync(post.MediaKey, imagePath);
            var html = PublishAssistantBuilder.WritePage(folder, post, imagePath);
            Process.Start(new ProcessStartInfo(html) { UseShellExecute = true });
            await client.ReportResultAsync(post.Id, "assistant_opened", "تم فتح صفحة المساعدة على جهاز المحل");
        }
        ShopStationText.Text = posts.Count == 0 ? "محطة المحل تعمل — لا يوجد منشورات مستحقة الآن" : $"محطة المحل فتحت {posts.Count} منشور للنشر";
    }
}
