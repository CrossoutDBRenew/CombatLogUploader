using System.Configuration;
using System.Data;
using System.Windows;
using Application = System.Windows.Application;

namespace CrossoutDBUploader;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
using System.Threading;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool created;
        _mutex = new Mutex(true, "CrossoutDBUploaderSingleton", out created);

        if (!created)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}