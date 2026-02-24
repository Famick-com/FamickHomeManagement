namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for monitoring network connectivity and server reachability.
/// </summary>
public class ConnectivityService : IDisposable
{
    private readonly ShoppingApiClient _apiClient;
    private bool _isOnline;
    private bool _disposed;
    private CancellationTokenSource? _healthCheckCts;

    public event EventHandler<bool>? ConnectivityChanged;

    public bool IsOnline
    {
        get => _isOnline;
        private set
        {
            if (_isOnline != value)
            {
                _isOnline = value;
                ConnectivityChanged?.Invoke(this, value);
            }
        }
    }

    public ConnectivityService(ShoppingApiClient apiClient)
    {
        _apiClient = apiClient;
        _isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Starts periodic health checks to verify server reachability.
    /// </summary>
    public void StartHealthChecks(TimeSpan interval)
    {
        StopHealthChecks();

        _healthCheckCts = new CancellationTokenSource();
        _ = RunHealthChecksAsync(interval, _healthCheckCts.Token);
    }

    /// <summary>
    /// Stops periodic health checks.
    /// </summary>
    public void StopHealthChecks()
    {
        _healthCheckCts?.Cancel();
        _healthCheckCts?.Dispose();
        _healthCheckCts = null;
    }

    /// <summary>
    /// Performs an immediate check of server reachability.
    /// </summary>
    public async Task<bool> CheckServerReachableAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            IsOnline = false;
            return false;
        }

        var reachable = await _apiClient.CheckHealthAsync();
        IsOnline = reachable;
        return reachable;
    }

    private async Task RunHealthChecksAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckServerReachableAsync();
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore errors and continue checking
            }
        }
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            // Network is available, verify server is reachable
            await CheckServerReachableAsync();
        }
        else
        {
            IsOnline = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopHealthChecks();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}
