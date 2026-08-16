namespace MgrCode.Backend.Services;

public interface IMainThreadDispatcher
{
    bool IsMainThread { get; }

    void Dispatch(Action action);
}
