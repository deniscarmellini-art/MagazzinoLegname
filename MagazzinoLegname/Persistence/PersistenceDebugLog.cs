using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

internal static class PersistenceDebugLog
{
    [Conditional("DEBUG")]
    public static void Write(string message) => Debug.WriteLine($"[SQL] {message}");

    [Conditional("DEBUG")]
    public static void WriteException(string operation, Exception exception)
    {
        Debug.WriteLine($"[SQL] {operation} FAILED");
        Debug.WriteLine($"[SQL] Exception: {exception.GetType().FullName}: {exception.Message}");
        Debug.WriteLine($"[SQL] StackTrace: {exception.StackTrace}");

        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
            Debug.WriteLine($"[SQL] InnerException: {inner.GetType().FullName}: {inner.Message}");

        var sqlException = exception is DbUpdateException updateException
            ? FindSqlException(updateException)
            : FindSqlException(exception);
        if (sqlException is not null)
            Debug.WriteLine($"[SQL] SqlException Number={sqlException.Number}; State={sqlException.State}; Class={sqlException.Class}; Message={sqlException.Message}");
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException sqlException)
                return sqlException;
        return null;
    }
}
