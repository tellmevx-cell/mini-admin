using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MiniAdmin.Infrastructure.Persistence;

public interface IDatabaseInitializationLock
{
    Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseInitializationLock(
    MiniAdminDbContext dbContext,
    IConfiguration configuration) : IDatabaseInitializationLock
{
    private const string LockName = "miniadmin:database-initialization";

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return NoOpAsyncDisposable.Instance;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var timeoutSeconds = ReadTimeoutSeconds(configuration);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT GET_LOCK(@lockName, @timeoutSeconds);";
            AddParameter(command, "@lockName", LockName);
            AddParameter(command, "@timeoutSeconds", timeoutSeconds);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (Convert.ToInt32(result) != 1)
            {
                throw new TimeoutException(
                    $"Timed out after {timeoutSeconds} seconds waiting for the database initialization lock.");
            }

            return new MySqlInitializationLease(connection, shouldClose);
        }
        catch
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }

            throw;
        }
    }

    private static int ReadTimeoutSeconds(IConfiguration configuration)
    {
        return int.TryParse(configuration["Database:InitializationLockTimeoutSeconds"], out var value)
            ? Math.Clamp(value, 10, 900)
            : 180;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed class MySqlInitializationLease(
        DbConnection connection,
        bool shouldClose) : IAsyncDisposable
    {
        private bool disposed;

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                if (connection.State == ConnectionState.Open)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT RELEASE_LOCK(@lockName);";
                    AddParameter(command, "@lockName", LockName);
                    await command.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            finally
            {
                if (shouldClose && connection.State != ConnectionState.Closed)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public static NoOpAsyncDisposable Instance { get; } = new();

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
