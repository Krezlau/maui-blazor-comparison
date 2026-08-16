using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

public interface IBybitService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticker>> GetTickersAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<Ticker> Subscribe(IEnumerable<string> symbols);
}
