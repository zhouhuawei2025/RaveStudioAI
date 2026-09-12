using HandyControl.Controls;
using HandyControl.Data;
using RaveStudioAI.Common;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI.Pages;

public partial class AIConfigPage : UserControl, INotifyPropertyChanged
{
    private bool _loadingApiKey;
    private AIConfigProfile? _selectedProfile;

    public ObservableCollection<AIConfigProfile> Profiles => AIConfigStore.Profiles;

    public AIConfigProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            _selectedProfile = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedProfile)));
            LoadApiKey();
        }
    }

    public AIConfigPage()
    {
        InitializeComponent();
        DataContext = this;
        SelectedProfile = AIConfigStore.Current;
        ProfileComboBox.SelectedItem = SelectedProfile;
        RefreshCurrentText();
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedProfile = ProfileComboBox.SelectedItem as AIConfigProfile;
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_loadingApiKey && SelectedProfile is not null)
        {
            SelectedProfile.ApiKey = ApiKeyPasswordBox.Password;
        }
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = new AIConfigProfile
        {
            Name = $"新配置 {Profiles.Count + 1}",
            Url = "https://api.example.com/v1",
            Model = "model-name",
            BatchSize = 20
        };
        Profiles.Add(profile);
        ProfileComboBox.SelectedItem = profile;
    }

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null) return;

        try
        {
            AIConfigStore.Apply(SelectedProfile);
            RefreshCurrentText();
            Growl.Success(new GrowlInfo { Message = "AI 配置已应用，下一次请求将使用此配置。", WaitTime = 3 });
        }
        catch (Exception ex)
        {
            Growl.Error(new GrowlInfo { Message = ex.Message, WaitTime = 4 });
        }
    }

    private void SaveProfiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SelectedProfile is not null)
            {
                AIConfigStore.Apply(SelectedProfile);
            }
            AIConfigStore.Save();
            RefreshCurrentText();
            Growl.Success(new GrowlInfo { Message = "全部 AI 配置已保存。", WaitTime = 3 });
        }
        catch (Exception ex)
        {
            Growl.Error(new GrowlInfo { Message = ex.Message, WaitTime = 4 });
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null) return;

        TestConnectionButton.IsEnabled = false;
        TestConnectionButton.Content = "正在测试...";
        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try
        {
            await AIHelper.TestConnectionAsync(SelectedProfile, timeout.Token);
            Growl.Success(new GrowlInfo
            {
                Message = $"AI 连接成功，响应耗时 {stopwatch.Elapsed.TotalSeconds:F1} 秒。",
                WaitTime = 4
            });
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Growl.Error(new GrowlInfo { Message = "AI 连接测试超时（1 分钟）。", WaitTime = 5 });
        }
        catch (Exception ex)
        {
            Growl.Error(new GrowlInfo { Message = $"AI 连接失败：{ex.Message}", WaitTime = 6 });
        }
        finally
        {
            stopwatch.Stop();
            TestConnectionButton.Content = "测试连接";
            TestConnectionButton.IsEnabled = true;
        }
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null || Profiles.Count <= 1)
        {
            Growl.Warning(new GrowlInfo { Message = "至少需要保留一个 AI 配置。", WaitTime = 3 });
            return;
        }

        var index = Profiles.IndexOf(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        ProfileComboBox.SelectedIndex = Math.Min(index, Profiles.Count - 1);
    }

    private void LoadApiKey()
    {
        if (ApiKeyPasswordBox is null) return;
        _loadingApiKey = true;
        ApiKeyPasswordBox.Password = SelectedProfile?.ApiKey ?? string.Empty;
        _loadingApiKey = false;
    }

    private void RefreshCurrentText()
    {
        CurrentConfigText.Text = $"当前使用：{AIConfigStore.Current.Name} · {AIConfigStore.Current.Model}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
