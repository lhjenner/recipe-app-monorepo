using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;

namespace RecipeApp.Api.ComponentTests.Backup;

internal static class ArchiveEncryption
{
    public static async Task<string> RecoverAsync(string archive, string directory)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "RecipeApp.slnx")))
            root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root not found.");
        var module = Path.Combine(root.FullName, "tools", "RecipeBackup", "encryption.mjs");
        var identity = Path.Combine(directory, "synthetic-identity.txt");
        var encrypted = Path.Combine(directory, "synthetic-recovery.age");
        var recovered = Path.Combine(directory, "decrypted-recovery.zip");
        await RunAsync("age-keygen", ["-o", identity]);
        var recipient = (await RunAsync("age-keygen", ["-y", identity])).Trim();
        var script = $$"""
            import { copyFile, rename, rm } from 'node:fs/promises';
            const { encryptBackup, verifyDownload } = await import({{JsonSerializer.Serialize(new Uri(module).AbsoluteUri)}});
            const result = await encryptBackup({
              recipient: {{JsonSerializer.Serialize(recipient)}},
              root: {{JsonSerializer.Serialize(directory)}}, maxBytes: 60 * 1024 * 1024,
              exportArchive: (path) => copyFile({{JsonSerializer.Serialize(archive)}}, path),
            });
            try {
              await verifyDownload(result.directory, result.sha256, result.bytes);
              await rename(result.path, {{JsonSerializer.Serialize(encrypted)}});
            } finally {
              await rm(result.directory, { recursive: true, force: true });
            }
            """;
        await RunAsync("node", ["--input-type=module", "--eval", script]);
        await RunAsync("age", ["--decrypt", "-i", identity, "-o", recovered, encrypted]);
        var originalHash = await HashAsync(archive);
        (await HashAsync(recovered)).Should().Equal(originalHash);
        File.Delete(identity);
        return recovered;
    }

    private static async Task<byte[]> HashAsync(string path)
    {
        await using var file = File.OpenRead(path);
        return await SHA256.HashDataAsync(file);
    }

    private static async Task<string> RunAsync(string command, string[] arguments)
    {
        var start = new ProcessStartInfo(command)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {command}.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Synthetic {command} encryption rehearsal failed (exit {process.ExitCode}).");
            return await output;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
