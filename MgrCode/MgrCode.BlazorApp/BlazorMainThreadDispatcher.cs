using MgrCode.Backend.Services;

namespace MgrCode.BlazorApp;

public sealed class BlazorMainThreadDispatcher : IMainThreadDispatcher
{
    private readonly object _gate = new();
    private SynchronizationContext? _captured;
    private int _capturedThreadId = -1;

    public BlazorMainThreadDispatcher()
    {
        CaptureCurrent();
    }

    public bool IsMainThread
    {
        get
        {
            lock (_gate)
                return _captured is not null && Thread.CurrentThread.ManagedThreadId == _capturedThreadId;
        }
    }

    public void Dispatch(Action action)
    {
        SynchronizationContext? ctx;
        lock (_gate)
        {
            CaptureCurrent();
            ctx = _captured;
        }

        if (ctx is null)
        {
            action();
            return;
        }

        ctx.Post(_ => action(), null);
    }

    private void CaptureCurrent()
    {
        lock (_gate)
        {
            if (_captured is not null)
                return;

            if (SynchronizationContext.Current is { } current)
            {
                _captured = current;
                _capturedThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }
    }
}
