using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Postgres advisory-lock implementation. Uses <c>pg_try_advisory_lock(int8)</c>
/// keyed on a stable 64-bit hash of the user id. Suitable for self-hosted
/// (no Redis dependency).
///
/// Acquisition is non-blocking; we poll <c>pg_try_advisory_lock</c> with a 50ms
/// backoff until either the lock is held or the timeout elapses.
/// Disposal calls <c>pg_advisory_unlock</c> on the same key.
///
/// Important: <c>pg_advisory_lock</c> is per-session in Postgres. We hold a dedicated
/// connection (separate from the EF DbContext's connection) for the lock's lifetime
/// so EF queries inside the critical section don't accidentally release the lock
/// when the EF connection is returned to the pool.
/// </summary>
public class PostgresUserAdvisoryLockService : IUserAdvisoryLockService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<PostgresUserAdvisoryLockService> _logger;

    public PostgresUserAdvisoryLockService(
        HomeManagementDbContext context,
        ILogger<PostgresUserAdvisoryLockService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IAsyncDisposable> AcquireAsync(
        Guid userId,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var lockKey = ComputeLockKey(userId);
        var connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "DbContext has no connection string — cannot acquire advisory lock.");

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var deadline = DateTime.UtcNow + timeout;
        var attempt = 0;
        while (true)
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            cmd.Parameters.AddWithValue("key", lockKey);
            var result = await cmd.ExecuteScalarAsync(ct);

            if (result is true)
            {
                _logger.LogDebug(
                    "Acquired Postgres advisory lock for user {UserId} (key {LockKey}, attempt {Attempt})",
                    userId, lockKey, attempt);
                return new PostgresAdvisoryLockHandle(connection, lockKey, _logger);
            }

            if (DateTime.UtcNow >= deadline)
            {
                await connection.DisposeAsync();
                throw new LockAcquisitionTimeoutException(userId, timeout);
            }

            attempt++;
            // 50ms fixed backoff — Postgres advisory locks resolve fast on contention,
            // and we cap per-attempt latency to keep the worst-case wait predictable.
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }
    }

    /// <summary>
    /// Maps a 16-byte Guid to a stable 64-bit signed integer for use as the
    /// <c>pg_try_advisory_lock</c> key. Stable across processes and instances —
    /// the same userId always produces the same key.
    /// </summary>
    private static long ComputeLockKey(Guid userId)
    {
        // Use a SHA-256 of the user-id bytes prefixed with a discriminator so we
        // never collide with other namespaces of advisory locks (e.g. tenant migration).
        var input = Encoding.UTF8.GetBytes($"user-lock:{userId:N}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        // Take the first 8 bytes as a signed long. Postgres int8 is signed.
        return BitConverter.ToInt64(hash[..8]);
    }

    private sealed class PostgresAdvisoryLockHandle : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly long _lockKey;
        private readonly ILogger _logger;
        private bool _released;

        public PostgresAdvisoryLockHandle(
            NpgsqlConnection connection,
            long lockKey,
            ILogger logger)
        {
            _connection = connection;
            _lockKey = lockKey;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_released) return;
            _released = true;

            try
            {
                await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _connection);
                cmd.Parameters.AddWithValue("key", _lockKey);
                await cmd.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to release Postgres advisory lock {LockKey} — connection close will release it implicitly",
                    _lockKey);
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
