using System.Windows;
using System.Windows.Threading;

namespace GameHMI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"未处理的异常:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

