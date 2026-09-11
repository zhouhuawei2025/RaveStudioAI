using RaveStudioAI.Pages;
using RaveStudioAI.Common;
using System.Windows;
using System.Windows.Controls;

namespace RaveStudioAI;

public partial class MainWindow : Window
{
    private Button? _selectedNavigationButton;
    private readonly HomePage _homePage = new();
    private readonly SdsPage _sdsPage = new();
    private readonly MatrixPage _matrixPage = new();
    private readonly OpenQueryPage _openQueryPage = new();
    private readonly SetDataPointVisiblePage _setDataPointVisiblePage = new();
    private readonly EcsManualPage _ecsManualPage = new();
    private readonly RwsPage _rwsPage = new();
    private readonly AIConfigPage _aiConfigPage = new();
    private readonly LogsPage _logsPage = new();

    public MainWindow()
    {
        InitializeComponent();
        PageHost.Content = _homePage;
        SelectNavigationButton(HomeNavigationButton);
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
            "OpenQuery" => _openQueryPage,
            "SetDataPointVisible" => _setDataPointVisiblePage,
            "EcsManual" => _ecsManualPage,
            "Rws" => _rwsPage,
            "AIConfig" => _aiConfigPage,
            "Logs" => _logsPage,
            _ => _sdsPage
        };
        SelectNavigationButton((Button)sender);
    }

    private void SelectNavigationButton(Button button)
    {
        if (_selectedNavigationButton is not null)
        {
            _selectedNavigationButton.ClearValue(BackgroundProperty);
            _selectedNavigationButton.ClearValue(ForegroundProperty);
            _selectedNavigationButton.ClearValue(FontWeightProperty);
        }

        _selectedNavigationButton = button;
        button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 243, 255));
        button.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 86, 240));
        button.FontWeight = FontWeights.SemiBold;
    }
}
