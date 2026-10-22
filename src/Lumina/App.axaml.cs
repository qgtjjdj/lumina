using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Lumina.Services;
using Lumina.ViewModels;
using Lumina.Views;

namespace Lumina;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = SettingsService.Load();
        RequestedThemeVariant = settings.Theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel(settings);
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
                StartupArgs = desktop.Args ?? []
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
