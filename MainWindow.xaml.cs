using RaveStudioAI.Pages;
using RaveStudioAI.Common;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI;

public partial class MainWindow : Window
{
    private readonly HomePage _homePage = new();
    private readonly SdsPage _sdsPage = new();
    private readonly MatrixPage _matrixPage = new();
    private readonly EditCheckPage _editCheckPage = new();
    private readonly RwsPage _rwsPage = new();
    private readonly AIConfigPage _aiConfigPage = new();
    private readonly LogsPage _logsPage = new();

    public MainWindow()
    {
        InitializeComponent();
        PageHost.Content = _homePage;
        Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(App.StartupWarning)) Notice.Warning(App.StartupWarning, 5);
        };
    }

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string pageName })
        {
            return;
        }

        PageHost.Content = pageName switch
        {
            "Home" => _homePage,
            "Matrix" => _matrixPage,
            "EditCheck" => _editCheckPage,
            "Rws" => _rwsPage,
            "AIConfig" => _aiConfigPage,
            "Logs" => _logsPage,
            _ => _sdsPage
        };
    }
}
