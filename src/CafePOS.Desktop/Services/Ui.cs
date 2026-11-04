using System.Windows.Threading;

namespace CafePOS.Desktop.Services;

public static class Ui
{
    public static void OnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(DispatcherPriority.Background, action);
    }
}
