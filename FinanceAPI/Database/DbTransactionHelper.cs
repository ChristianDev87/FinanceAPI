using System.Data;

namespace FinanceAPI.Database;

/// <summary>
/// Executes a unit of work inside a Serializable database transaction.
/// Retries automatically on transient concurrency failures (serialization conflicts,
/// deadlocks, SQLite busy) so callers don't need per-provider error handling.
/// </summary>
internal static class DbTransactionHelper
{
    internal static async Task<T> ExecuteInSerializableTransactionAsync<T>(
        IDbConnection conn,
        Func<IDbTransaction, Task<T>> work,
        CancellationToken cancellationToken = default,
        int maxRetries = 3)
    {
        const int baseDelayMs = 50;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using IDbTransaction txn = conn.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                T result = await work(txn);
                txn.Commit();
                return result;
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < maxRetries)
            {
                txn.Rollback();
                await Task.Delay(baseDelayMs * attempt, cancellationToken);
            }
            catch
            {
                txn.Rollback();
                throw;
            }
        }

        throw new InvalidOperationException("Serializable transaction failed after maximum retries.");
    }

    internal static Task ExecuteInSerializableTransactionAsync(
        IDbConnection conn,
        Func<IDbTransaction, Task> work,
        CancellationToken cancellationToken = default,
        int maxRetries = 3)
        => ExecuteInSerializableTransactionAsync<bool>(
            conn,
            async txn => { await work(txn); return true; },
            cancellationToken,
            maxRetries);

    /// <summary>
    /// Returns true for transient concurrency errors that are safe to retry:
    /// PostgreSQL serialization failure (40001), MySQL deadlock / lock timeout,
    /// SQLite busy (5) or locked (6).
    /// </summary>
    private static bool IsRetryable(Exception ex) =>
        (ex is Npgsql.PostgresException pg && pg.SqlState == "40001") ||
        (ex is MySqlConnector.MySqlException my &&
            my.ErrorCode is MySqlConnector.MySqlErrorCode.LockDeadlock
                         or MySqlConnector.MySqlErrorCode.LockWaitTimeout) ||
        (ex is Microsoft.Data.Sqlite.SqliteException sq &&
            (sq.SqliteErrorCode == 5 || sq.SqliteErrorCode == 6));
}
