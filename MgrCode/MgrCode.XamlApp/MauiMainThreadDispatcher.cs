using MgrCode.Backend.Services;

namespace MgrCode.XamlApp;

public sealed class MauiMainThreadDispatcher : IMainThreadDispatcher
{
    public bool IsMainThread => MainThread.IsMainThread;

    public void Dispatch(Action action)
        => MainThread.BeginInvokeOnMainThread(action);
}
