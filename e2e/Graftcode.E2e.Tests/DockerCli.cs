using System.Diagnostics;

namespace Graftcode.E2e.Tests;

internal static class DockerCli
{
    public const string UnavailableReason =
        "Docker is not available, so this e2e test was not executed. Install Docker and rerun dotnet test e2e/Graftcode.E2e.Tests/Graftcode.E2e.Tests.csproj.";

    public static async Task<bool> IsDaemonAvailable()
    {
        try
        {
            var result = await Run(TimeSpan.FromSeconds(20), CancellationToken.None, "info");
            return result.Code == 0;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static async Task RunChecked(TimeSpan timeout, CancellationToken ct, params string[] args)
    {
        var result = await Run(timeout, ct, args);
        if (result.Code != 0)
        {
            throw new InvalidOperationException(
                $"docker {string.Join(' ', args)} exited {result.Code}.{Environment.NewLine}{result.Stdout}{result.Stderr}");
        }
    }

    public static async Task<CommandResult> Run(TimeSpan timeout, CancellationToken ct, params string[] args)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start docker.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException($"docker {string.Join(' ', args)} exceeded {timeout}.");
        }

        return new CommandResult(process.ExitCode, await stdout, await stderr);
    }
}

internal readonly record struct CommandResult(int Code, string Stdout, string Stderr);

internal sealed class RunningGateway : IAsyncDisposable
{
    private readonly string _containerName;

    private RunningGateway(string containerName)
    {
        _containerName = containerName;
    }

    public static async Task<RunningGateway> StartAsync(
        string dockerfile,
        string contextDirectory,
        string imageTag,
        string containerName,
        CancellationToken ct)
    {
        await DockerCli.Run(TimeSpan.FromSeconds(30), ct, "rm", "-f", containerName);
        await DockerCli.RunChecked(
            TimeSpan.FromMinutes(12),
            ct,
            "build",
            "--pull",
            "-f",
            dockerfile,
            "-t",
            imageTag,
            contextDirectory);
        await DockerCli.RunChecked(
            TimeSpan.FromMinutes(2),
            ct,
            "run",
            "-d",
            "--name",
            containerName,
            "-p",
            "80:80",
            "-p",
            "81:81",
            imageTag);
        return new RunningGateway(containerName);
    }

    public async Task<string> LogsAsync()
    {
        var result = await DockerCli.Run(TimeSpan.FromSeconds(30), CancellationToken.None, "logs", _containerName);
        return result.Stdout + result.Stderr;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DockerCli.Run(TimeSpan.FromSeconds(30), CancellationToken.None, "rm", "-f", _containerName);
        }
        catch (Exception)
        {
        }
    }
}
