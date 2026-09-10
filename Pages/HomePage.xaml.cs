using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using RaveStudioAI.Common;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI.Pages;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
        DataContext = CurrentProject.Instance;
    }

    private void UploadSds_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择经过人工确认的 Rave SDS",
            Filter = "Rave SDS Excel|*.xlsx;*.xlsm|Excel|*.xlsx;*.xlsm",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var result = SdsWorkbookReader.Read(dialog.FileName);
            CurrentProject.Instance.Replace(dialog.FileName, result);
            Growl.Success(new GrowlInfo
            {
                Message = $"SDS 加载成功：{result.Forms.Count} Forms，{result.Fields.Count} Fields，{result.Folders.Count} Folders。",
                WaitTime = 3
            });
        }
        catch (Exception ex)
        {
            Notice.Error($"SDS 读取失败：{ex.Message}");
        }
    }
}
