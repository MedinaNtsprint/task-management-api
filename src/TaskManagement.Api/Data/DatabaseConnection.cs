using Npgsql;

namespace TaskManagement.Api.Data;

internal static class DatabaseConnection
{
    private const string PasswordPath = "/run/db-secret/password";

    internal static NpgsqlDataSource CreateDataSource()
    {
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "db",
            Database = "tasks",
            Username = "tasks"
        };

        var builder = new NpgsqlDataSourceBuilder(connection.ConnectionString);
        builder.UsePasswordProvider(
            _ => ReadPassword(),
            (_, cancellationToken) => ReadPasswordAsync(cancellationToken));
        return builder.Build();
    }

    private static string ReadPassword()
    {
        try
        {
            return ValidatePassword(File.ReadAllText(PasswordPath));
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException("The database password file is missing.");
        }
    }

    private static async ValueTask<string> ReadPasswordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var password = await File.ReadAllTextAsync(PasswordPath, cancellationToken);
            return ValidatePassword(password);
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException("The database password file is missing.");
        }
    }

    private static string ValidatePassword(string contents)
    {
        var password = contents.Trim();
        return password.Length > 0
            ? password
            : throw new InvalidOperationException("The database password file is empty.");
    }
}
