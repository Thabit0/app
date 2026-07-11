using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DawishContentStudio.Core;
using WpfControls = System.Windows.Controls;

namespace DawishContentStudio.Manager;

public partial class MainWindow : Window
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private readonly ObservableCollection<LocalImageItem> _images = new();
    private readonly AppSettingsStore _settingsStore = new();
    private AppSettings _settings = new();
    private CancellationTokenSource? _shopCts;
    private DeviceRole _selectedRole = DeviceRole.Admin;

    public MainWindow()
    {
        InitializeComponent();
        ImagesListBox.ItemsSource = _images;
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        FillTimeCombos();
        _settings = _settingsStore.Load();
        _selectedRole = _settings.Role;
        WorkerUrlTextBox.Text = _settings.WorkerUrl;
        TokenPasswordBox.Password = _settings.Token;
        UpdateRoleUi();
        ShowTab(string.IsNullOrWhiteSpace(_settings.Token) ? 0 : (_settings.Role == DeviceRole.Shop ? 2 : 1));
    }

    private void FillTimeCombos()
    {
        for (var i = 1; i <= 12; i++) HourComboBox.Items.Add(i.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < 60; i += 5) MinuteComboBox.Items.Add(i.ToString("00", CultureInfo.InvariantCulture));
        HourComboBox.SelectedItem = "7";
        MinuteComboBox.SelectedItem = "00";
    }

    private void ShowTab(int index)
    {
        MainTabs.SelectedIndex = index;
        (PageTitleText.Text, PageHintText.Text) = index switch
        {
            0 => ("الدخول", "اختر نوع الجهاز وأدخل الرمز مرة واحدة."),
            1 => ("الصور والجدولة", "استورد مجلدًا كاملًا، وكل صورة ستصبح منشورًا مستقلًا."),
            2 => ("جهاز المحل", "شغّل المحطة واترك الحسابات مسجلة على هذا الجهاز."),
            _ => ("Dawish Content Studio", "")
        };
    }

    private void ShowLogin_Click(object sender, RoutedEventArgs e) => ShowTab(0);
    private void ShowWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRole == DeviceRole.Shop)
        {
            MessageBox.Show("هذه الصفحة للمدير فقط.");
            return;
        }
        ShowTab(1);
    }
    private void ShowShop_Click(object sender, RoutedEventArgs e) => ShowTab(2);

    private void SetAdminRole_Click(object sender, RoutedEventArgs e)
    {
        _selectedRole = DeviceRole.Admin;
        UpdateRoleUi();
    }

    private void SetShopRole_Click(object sender, RoutedEventArgs e)
    {
        _selectedRole = DeviceRole.Shop;
        UpdateRoleUi();
    }

    private void UpdateRoleUi()
    {
        RoleBadgeText.Text = _selectedRole == DeviceRole.Shop ? "الوضع: جهاز المحل" : "الوضع: المدير";
        NavWorkspace.IsEnabled = _selectedRole != DeviceRole.Shop;
        AdminRoleButton.BorderThickness = _selectedRole == DeviceRole.Admin ? new Thickness(3) : new Thickness(1);
        ShopRoleButton.BorderThickness = _selectedRole == DeviceRole.Shop ? new Thickness(3) : new Thickness(1);
    }

    private void SaveSettings()
    {
        var url = WorkerUrlTextBox.Text.Trim().TrimEnd('/');
        var token = TokenPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("ضع رابط Worker أولًا.");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("ضع الرمز أولًا.");
        _settings = new AppSettings { WorkerUrl = url, Token = token, Role = _selectedRole };
        _settingsStore.Save(_settings);
    }

    private CloudflareClient Client()
    {
        var url = WorkerUrlTextBox.Text.Trim().TrimEnd('/');
        var token = TokenPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("ضع رابط Worker أولًا.");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("ضع الرمز أولًا.");
        return new CloudflareClient(url, token);
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings();
            var health = await Client().HealthAsync();
            ConnectionStatusText.Text = "Cloudflare: متصل";
            MessageBox.Show(health.Message, "تم الاتصال");
            ShowTab(_selectedRole == DeviceRole.Shop ? 2 : 1);
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "Cloudflare: مشكلة";
            MessageBox.Show(ex.Message, "فشل الاتصال");
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRole == DeviceRole.Shop) await RefreshDuePostsAsync();
        else ConnectionStatusText.Text = "Cloudflare: جاهز";
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "اختر مجلد الصور", Multiselect = false };
        if (dialog.ShowDialog() == true) ImportFolder(dialog.FolderName);
    }

    private void ChooseImages_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "الصور|*.jpg;*.jpeg;*.png;*.webp",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
    }

    private void Workspace_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Workspace_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (var path in paths)
        {
            if (Directory.Exists(path)) ImportFolder(path);
            else if (File.Exists(path)) AddFiles([path]);
        }
    }

    private void ImportFolder(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f), StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        AddFiles(files);
        UploadStatusText.Text = files.Length == 0 ? "لم أجد صورًا في هذا المجلد." : $"تم استيراد {files.Length} صورة من المجلد.";
    }

    private void AddFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (!ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
            if (_images.Any(x => string.Equals(x.Path, file, StringComparison.OrdinalIgnoreCase))) continue;
            _images.Add(new LocalImageItem { Path = file, FileName = Path.GetFileName(file), Status = "جاهز" });
        }
        if (_images.Count > 0 && ImagesListBox.SelectedIndex < 0) ImagesListBox.SelectedIndex = 0;
        UpdateImageCount();
    }

    private void UpdateImageCount()
    {
        ImageCountText.Text = _images.Count == 0
            ? "اسحب مجلدًا هنا أو اختره. كل صورة تتحول إلى منشور مستقل."
            : $"{_images.Count} صورة — كل صورة منشور مستقل";
    }

    private void ImagesListBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        var count = ImagesListBox.SelectedItems.Count;
        SelectionSummaryText.Text = count == 0 ? "حدد صورة أو عدة صور" : $"تم تحديد {count} منشور";
        EditTitleText.Text = count > 1 ? "تعديل جماعي" : "إعدادات المنشور";
        LoadSelectedImage();
    }

    private void LoadSelectedImage()
    {
        if (ImagesListBox.SelectedItem is not LocalImageItem item || !File.Exists(item.Path))
        {
            SelectedImagePreview.Source = null;
            return;
        }
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(item.Path);
            image.EndInit();
            SelectedImagePreview.Source = image;
        }
        catch { SelectedImagePreview.Source = null; }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ImagesListBox.SelectAll();
    private void ClearSelection_Click(object sender, RoutedEventArgs e) => ImagesListBox.UnselectAll();

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ImagesListBox.SelectedItems.Cast<LocalImageItem>().ToList();
        foreach (var item in selected) _images.Remove(item);
        UpdateImageCount();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A && MainTabs.SelectedIndex == 1)
        {
            ImagesListBox.SelectAll(); e.Handled = true;
        }
        else if (e.Key == Key.Delete && MainTabs.SelectedIndex == 1)
        {
            DeleteSelected_Click(sender, e); e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O && MainTabs.SelectedIndex == 1)
        {
            ChooseFolder_Click(sender, e); e.Handled = true;
        }
    }

    private void PostFields_Changed(object sender, RoutedEventArgs e) { }

    private DateTime BuildScheduledAt(int offsetMinutes)
    {
        var date = ScheduleDatePicker.SelectedDate ?? DateTime.Today;
        var hour = int.TryParse(HourComboBox.SelectedItem?.ToString(), out var h) ? h : 7;
        var minute = int.TryParse(MinuteComboBox.SelectedItem?.ToString(), out var m) ? m : 0;
        var isPm = (AmPmComboBox.SelectedItem as WpfControls.ComboBoxItem)?.Content?.ToString() == "م";
        if (hour == 12) hour = 0;
        if (isPm) hour += 12;
        return date.Date.AddHours(hour).AddMinutes(minute).AddMinutes(offsetMinutes);
    }

    private int SpacingMinutes()
    {
        var item = SpacingMinutesComboBox.SelectedItem as WpfControls.ComboBoxItem;
        return int.TryParse(item?.Tag?.ToString(), out var value) ? value : 60;
    }

    private PostUploadRequest BuildRequest(DateTime scheduledAt)
    {
        var caption = CaptionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(caption)) throw new InvalidOperationException("اكتب الكابشن أولًا.");
        var violations = new MedicalClaimsGuard().FindViolations(caption);
        if (violations.Count > 0) throw new InvalidOperationException("الكابشن يحتوي ادعاءً طبيًا ممنوعًا: " + string.Join("، ", violations));
        if (InstagramCheckBox.IsChecked != true && TikTokCheckBox.IsChecked != true && SnapchatCheckBox.IsChecked != true)
            throw new InvalidOperationException("اختر منصة واحدة على الأقل.");
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

    private async void ScheduleSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ImagesListBox.SelectedItems.Cast<LocalImageItem>().ToList();
        if (selected.Count == 0) { MessageBox.Show("حدد صورة أو عدة صور أولًا."); return; }
        await UploadBatchAsync(selected);
    }

    private async void ScheduleAllImages_Click(object sender, RoutedEventArgs e)
    {
        if (_images.Count == 0) { MessageBox.Show("استورد مجلد الصور أولًا."); return; }
        await UploadBatchAsync(_images.ToList());
    }

    private async Task UploadBatchAsync(IReadOnlyList<LocalImageItem> items)
    {
        try
        {
            SaveSettings();
            var confirmation = MessageBox.Show($"سيتم إنشاء {items.Count} منشور مستقل. هل نبدأ؟", "تأكيد الجدولة", MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                UploadStatusText.Text = $"جاري رفع {i + 1} من {items.Count}: {item.FileName}";
                var result = await Client().UploadPostAsync(BuildRequest(BuildScheduledAt(i * SpacingMinutes())), item.Path);
                item.Status = "مجدول";
                item.RemoteId = result.Id;
                ImagesListBox.Items.Refresh();
            }
            UploadStatusText.Text = $"تمت جدولة {items.Count} منشور مستقل بنجاح.";
            MessageBox.Show("اكتملت الجدولة.");
        }
        catch (Exception ex)
        {
            UploadStatusText.Text = "توقفت العملية: " + ex.Message;
            MessageBox.Show(ex.Message, "خطأ");
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
                DuePostsListBox.Items.Add($"{p.ScheduledAt.ToLocalTime():dd/MM/yyyy hh:mm tt} — {p.Caption}");
            ConnectionStatusText.Text = "Cloudflare: متصل";
        }
        catch (Exception ex)
        {
            DuePostsListBox.Items.Clear();
            DuePostsListBox.Items.Add("فشل الجلب: " + ex.Message);
        }
    }

    private void StartShopStation_Click(object sender, RoutedEventArgs e)
    {
        try { _selectedRole = DeviceRole.Shop; SaveSettings(); }
        catch (Exception ex) { MessageBox.Show(ex.Message); return; }
        if (_shopCts is not null) { MessageBox.Show("المحطة تعمل بالفعل."); return; }
        _shopCts = new CancellationTokenSource();
        ShopStationText.Text = "المحطة تعمل — اترك الجهاز مفتوحًا";
        _ = ShopLoopAsync(_shopCts.Token);
    }

    private async Task ShopLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await RunShopCycleAsync(); await Task.Delay(TimeSpan.FromSeconds(45), token); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShopStationText.Text = "خطأ: " + ex.Message);
                try { await Task.Delay(TimeSpan.FromSeconds(45), token); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private void StopShopStation_Click(object sender, RoutedEventArgs e)
    {
        _shopCts?.Cancel(); _shopCts = null; ShopStationText.Text = "المحطة متوقفة";
    }

    private async void RunShopOnce_Click(object sender, RoutedEventArgs e) => await RunShopCycleAsync();

    private async Task RunShopCycleAsync()
    {
        await Client().HeartbeatAsync(Environment.MachineName, "shop");
        var posts = await Client().GetDuePostsAsync();
        DuePostsListBox.Items.Clear();
        foreach (var post in posts)
        {
            DuePostsListBox.Items.Add("مستحق الآن: " + post.Caption);
            var folder = Path.Combine(_settingsStore.AppFolder, "shop-posts", post.Id);
            Directory.CreateDirectory(folder);
            var imagePath = Path.Combine(folder, "image.jpg");
            await Client().DownloadMediaAsync(post.MediaKey, imagePath);
            var html = PublishAssistantBuilder.WritePage(folder, post, imagePath);
            Process.Start(new ProcessStartInfo(html) { UseShellExecute = true });
            await Client().ReportResultAsync(post.Id, "assistant_opened", "تم فتح صفحة المساعدة على جهاز المحل");
        }
        ShopStationText.Text = posts.Count == 0 ? "المحطة تعمل — لا توجد منشورات مستحقة" : $"تم فتح {posts.Count} منشور للنشر";
    }
}
