namespace Famick.HomeManagement.Core.Messaging;

/// <summary>
/// Collects multiple IDisposable subscriptions and disposes them all at once.
/// </summary>
public sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public void Add(IDisposable disposable) => _disposables.Add(disposable);

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
        _disposables.Clear();
    }
}
