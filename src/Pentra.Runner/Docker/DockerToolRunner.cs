using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;

namespace Pentra.Runner.Docker;

/// <summary>
/// Runs a tool in an ephemeral, hardened Docker container via the Docker Engine
/// API (no shell, no CLI). The only component that touches Docker. Arguments are
/// passed as a vector; nothing is concatenated into a shell string.
/// </summary>
public sealed class DockerToolRunner : IToolRunner
{
    private readonly IDockerClient _docker;
    private readonly ILogger<DockerToolRunner> _logger;

    public DockerToolRunner(IDockerClient docker, ILogger<DockerToolRunner> logger)
    {
        _docker = docker;
        _logger = logger;
    }

    public async Task<ContainerRunResult> RunAsync(ContainerRunSpec spec, Func<LogStream, string, Task> onLog, CancellationToken ct)
    {
        try
        {
            await EnsureImageAsync(spec.ImageRef, onLog, ct);
        }
        catch (Exception ex)
        {
            return new ContainerRunResult(-1, false, false, $"Failed to pull image '{spec.ImageRef}': {ex.Message}");
        }

        string? containerId = null;
        try
        {
            var create = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = spec.ImageRef,
                Cmd = spec.Arguments.ToList(),
                AttachStdout = true,
                AttachStderr = true,
                HostConfig = BuildHostConfig(spec.Limits)
            }, ct);
            containerId = create.ID;

            using var logStream = await _docker.Containers.GetContainerLogsAsync(
                containerId,
                tty: false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = true },
                ct);

            await _docker.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);

            await PumpLogsAsync(logStream, onLog, ct);

            var wait = await _docker.Containers.WaitContainerAsync(containerId, ct);
            return new ContainerRunResult((int)wait.StatusCode, false, false, wait.Error?.Message ?? "");
        }
        catch (OperationCanceledException)
        {
            await TryStopAsync(containerId);
            return new ContainerRunResult(-1, TimedOut: false, Cancelled: true, "Execution stopped.");
        }
        catch (DockerApiException ex)
        {
            await TryStopAsync(containerId);
            return new ContainerRunResult(-1, false, false, ex.Message);
        }
        finally
        {
            await TryRemoveAsync(containerId);
        }
    }

    private static HostConfig BuildHostConfig(ExecutionLimits limits) => new()
    {
        Memory = limits.MemoryBytes,
        NanoCPUs = (long)(limits.Cpus * 1_000_000_000),
        PidsLimit = limits.PidsLimit,
        NetworkMode = limits.Network == ContainerNetwork.None ? "none" : "bridge",
        CapDrop = new List<string> { "ALL" },
        SecurityOpt = new List<string> { "no-new-privileges" },
        // No bind mounts, never privileged. AutoRemove off so we read the exit code first.
        AutoRemove = false,
        Privileged = false
    };

    private async Task EnsureImageAsync(string imageRef, Func<LogStream, string, Task> onLog, CancellationToken ct)
    {
        try
        {
            await _docker.Images.InspectImageAsync(imageRef, ct);
            return; // already present
        }
        catch (DockerImageNotFoundException)
        {
            // fall through to pull
        }

        await onLog(LogStream.System, $"Pulling image {imageRef} …");

        var (repo, tag) = SplitImage(imageRef);
        await _docker.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = repo, Tag = tag },
            authConfig: null,
            new Progress<JSONMessage>(_ => { }),
            ct);

        await onLog(LogStream.System, "Image ready.");
    }

    private static (string repo, string tag) SplitImage(string imageRef)
    {
        var idx = imageRef.LastIndexOf(':');
        // Guard against a port in a registry host (no '/' after ':') — our images have none.
        return idx > 0 && !imageRef[(idx + 1)..].Contains('/')
            ? (imageRef[..idx], imageRef[(idx + 1)..])
            : (imageRef, "latest");
    }

    private static async Task PumpLogsAsync(MultiplexedStream stream, Func<LogStream, string, Task> onLog, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        while (true)
        {
            var read = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
            if (read.EOF)
            {
                break;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, read.Count);
            var isErr = read.Target == MultiplexedStream.TargetStream.StandardError;
            await AppendAndEmitAsync(isErr ? stderr : stdout, text, isErr ? LogStream.Stderr : LogStream.Stdout, onLog);
        }

        await FlushAsync(stdout, LogStream.Stdout, onLog);
        await FlushAsync(stderr, LogStream.Stderr, onLog);
    }

    private static async Task AppendAndEmitAsync(StringBuilder acc, string text, LogStream stream, Func<LogStream, string, Task> onLog)
    {
        acc.Append(text);
        int newline;
        while ((newline = IndexOfNewline(acc)) >= 0)
        {
            var line = acc.ToString(0, newline).TrimEnd('\r');
            acc.Remove(0, newline + 1);
            await onLog(stream, line);
        }
    }

    private static int IndexOfNewline(StringBuilder sb)
    {
        for (var i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '\n') return i;
        }
        return -1;
    }

    private static async Task FlushAsync(StringBuilder acc, LogStream stream, Func<LogStream, string, Task> onLog)
    {
        if (acc.Length > 0)
        {
            await onLog(stream, acc.ToString().TrimEnd('\r'));
            acc.Clear();
        }
    }

    private async Task TryStopAsync(string? containerId)
    {
        if (containerId is null) return;
        try
        {
            await _docker.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 3 }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop container {ContainerId}", containerId);
        }
    }

    private async Task TryRemoveAsync(string? containerId)
    {
        if (containerId is null) return;
        try
        {
            await _docker.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove container {ContainerId}", containerId);
        }
    }
}
