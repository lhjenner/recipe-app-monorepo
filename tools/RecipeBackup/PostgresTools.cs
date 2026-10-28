using System.Diagnostics;
using Npgsql;

namespace RecipeBackup;

public sealed class PostgresTools(string? testContainerId = null)
{
    public async Task DumpAsync(string connectionString, string snapshot, string path, CancellationToken token)
    {
        await using var output = PrivateFiles.CreateNew(path);
        await RunAsync("pg_dump", connectionString,
            ["--format=custom", "--no-owner", "--no-acl", "--no-password", "--snapshot", snapshot], null, output, token);
    }

    public async Task RestoreAsync(string connectionString, string path, CancellationToken token)
    {
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database
            ?? throw new InvalidOperationException("The destination database name is required.");
        await using var input = File.OpenRead(path);
        await RunAsync("pg_restore", connectionString,
            ["--no-owner", "--no-acl", "--no-password", "--exit-on-error", "--single-transaction", "--dbname", database],
            input, Stream.Null, token);
    }

    private async Task RunAsync(
        string tool, string connectionString, string[] arguments,
        Stream? input, Stream output, CancellationToken token)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        var environment = new Dictionary<string, string>
        {
            ["PGHOST"] = testContainerId is null ? connection.Host! : "localhost",
            ["PGPORT"] = testContainerId is null ? connection.Port.ToString() : "5432",
            ["PGDATABASE"] = connection.Database!,
            ["PGUSER"] = connection.Username!,
            ["PGPASSWORD"] = connection.Password!,
            ["PGCHANNELBINDING"] = connection.ChannelBinding.ToString().ToLowerInvariant(),
            ["PGSSLMODE"] = testContainerId is not null ? "disable" : connection.SslMode switch
            {
                SslMode.VerifyFull => "verify-full",
                SslMode.VerifyCA => "verify-ca",
                SslMode.Require => "require",
                SslMode.Disable => "disable",
                _ => "prefer"
            }
        };
        var start = new ProcessStartInfo(testContainerId is null ? tool : "docker")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, CreateNoWindow = true
        };
        foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        foreach (var key in new[] { "PGSERVICE", "PGSERVICEFILE", "PGOPTIONS" })
            start.Environment.Remove(key);
        if (testContainerId is not null)
        {
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add("-i");
            foreach (var key in environment.Keys)
            {
                start.ArgumentList.Add("-e");
                start.ArgumentList.Add(key);
            }
            start.ArgumentList.Add(testContainerId);
            start.ArgumentList.Add(tool);
        }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        try
        {
            // Drain diagnostics but do not expose source data, server details, or credentials.
            var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, token);
            var read = process.StandardOutput.BaseStream.CopyToAsync(output, token);
            if (input is not null) await input.CopyToAsync(process.StandardInput.BaseStream, token);
            process.StandardInput.Close();
            await Task.WhenAll(read, errors, process.WaitForExitAsync(token));
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{tool} failed with exit code {process.ExitCode}; no backup/restore success is claimed.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
