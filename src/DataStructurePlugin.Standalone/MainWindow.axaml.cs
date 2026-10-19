using Avalonia.Controls;
using DataStructurePlugin.Features.Main;
using MyAvaloniaManagement.PluginSdk;

namespace DataStructurePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var document = new MainDocument();
        document.InitializeAsync(
            new NewDocumentActivation("DataStructurePlugin Standalone"),
            CancellationToken.None).GetAwaiter().GetResult();
        DataContext = document;
    }
}
