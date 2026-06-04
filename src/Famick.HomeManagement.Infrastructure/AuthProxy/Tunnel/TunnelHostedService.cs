using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;

/// <summary>
/// Owns the home server's outbound tunnel lifecycle. Polls for pairing
/// config; once paired, builds + runs a <see cref="TunnelClient"/>
/// and reconnects with exponential backoff (1s → 60s) when the
/// connection drops. Implements <see cref="ITunnelSender"/> so other
/// services can push frames over the current connection without
/// holding a direct reference to the client.
/// </summary>
public sealed class TunnelHostedService : BackgroundService, ITunnelSender
{
    private static readonly TimeSpan UnpairedPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITunnelRequestDispatcher _dispatcher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TunnelHostedService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHostApplicationLifetime _appLifetime;

    private volatile ITunnelClient? _currentClient;

    public TunnelHostedService(
        IServiceScopeFactory scopeFactory,
        ITunnelRequestDispatcher dispatcher,
        IConfiguration configuration,
        ILogger<TunnelHostedService> logger,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime appLifetime)
    {
        _scopeFactory = scopeFactory;
        _dispatcher = dispatcher;
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _appLifetime = appLifetime;
    }

    public async Task<bool> TrySendAsync(TunnelEnvelope envelope, CancellationToken ct = default)
    {
        var client = _currentClient;
        if (client is null || !client.IsConnected)
        {
            return false;
        }
        try
        {
            await client.SendAsync(envelope, ct).ConfigureAwait(false);
            return true;
        }
        catch (InvalidOperationException)
        {
            // Raced disconnect — treat as transient drop.
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Kestrel to bind before we try the loopback dispatcher
        // (the request dispatcher reads IServer addresses, which is empty
        // until the host is fully started).
        await WaitForApplicationStartedAsync(stoppingToken).ConfigureAwait(false);

        var backoff = InitialBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            TunnelConfig? config;
            try
            {
                config = await TryResolveConfigAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tunnel config resolve failed; will retry");
                config = null;
            }

            if (config is null)
            {
                await SafeDelayAsync(UnpairedPollInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var client = new TunnelClient(
                config.TunnelUrl,
                config.HomeServerId,
                config.PublicKeyPem,
                config.Rsa,
                _dispatcher,
                _loggerFactory.CreateLogger<TunnelClient>());

            _currentClient = client;

            try
            {
                await client.RunAsync(stoppingToken).ConfigureAwait(false);
                // Clean disconnect (e.g. server closed) → use the
                // initial backoff so a single-flap doesn't ramp us up.
                backoff = InitialBackoff;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Tunnel connection ended with exception; reconnecting in {Backoff}s",
                    backoff.TotalSeconds);
            }
            finally
            {
                _currentClient = null;
                config.Rsa.Dispose();
            }

            await SafeDelayAsync(backoff, stoppingToken).ConfigureAwait(false);
            backoff = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, backoff.Ticks * 2));
        }
    }

    /// <summary>
    /// Pulls <see cref="AuthProxyPairingConfig"/> + the JWT signing key
    /// from a fresh scope. Returns null when the home server isn't paired.
    /// </summary>
    private async Task<TunnelConfig?> TryResolveConfigAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        var signing = scope.ServiceProvider.GetRequiredService<IJwtSigningKeyService>();

        var pairing = await db.AuthProxyPairingConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (pairing is null)
        {
            return null;
        }

        // Convert the AuthProxy HTTP(S) base URL into the WebSocket
        // endpoint at /tunnel. Override via config when running against
        // a local AuthProxy on a different scheme.
        var tunnelUrlString = _configuration["AuthProxy:TunnelUrl"]
            ?? DeriveTunnelUrl(pairing.AuthProxyBaseUrl);

        if (!Uri.TryCreate(tunnelUrlString, UriKind.Absolute, out var tunnelUrl))
        {
            _logger.LogError(
                "Configured AuthProxy:TunnelUrl '{Url}' is not a valid absolute URL; tunnel disabled",
                tunnelUrlString);
            return null;
        }

        // Take ownership of an RSA instance bound to the current key.
        // The TunnelHostedService disposes it when the connection ends.
        var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(signing.SecurityKey.Rsa.ExportRSAPrivateKeyPem());
        var publicKeyPem = signing.SecurityKey.Rsa.ExportSubjectPublicKeyInfoPem();

        return new TunnelConfig(tunnelUrl, pairing.AuthProxyHomeServerId, publicKeyPem, rsa);
    }

    private static string DeriveTunnelUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "wss://" + trimmed["https://".Length..] + "/tunnel";
        }
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "ws://" + trimmed["http://".Length..] + "/tunnel";
        }
        return trimmed + "/tunnel";
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken stoppingToken)
    {
        if (_appLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var startedRegistration = _appLifetime.ApplicationStarted.Register(() => tcs.TrySetResult())
            .ConfigureAwait(false);
        await using var stopRegistration = stoppingToken.Register(() => tcs.TrySetCanceled())
            .ConfigureAwait(false);
        try
        {
            await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down before ever starting — caller handles.
        }
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — fine.
        }
    }

    private sealed record TunnelConfig(
        Uri TunnelUrl,
        Guid HomeServerId,
        string PublicKeyPem,
        System.Security.Cryptography.RSA Rsa);
}
