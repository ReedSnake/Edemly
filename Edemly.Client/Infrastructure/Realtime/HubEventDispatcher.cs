using System.Windows;
namespace Edemly.Client.Infrastructure.Realtime
{
    internal static class HubEventDispatcher
    {
        public static void Dispatch(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null ||
                dispatcher.HasShutdownStarted ||
                dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action);
        }
    }
}