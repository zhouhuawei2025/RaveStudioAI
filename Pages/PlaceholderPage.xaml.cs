using System.Windows.Controls;

namespace RaveStudioAI.Pages;

public partial class PlaceholderPage : UserControl
{
    public PlaceholderPage(string title, string description)
    {
        InitializeComponent();
        TitleText.Text = title;
        DescriptionText.Text = description;
    }
}
