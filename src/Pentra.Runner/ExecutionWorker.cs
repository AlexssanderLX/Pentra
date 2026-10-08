using Microsoft.Extensions.DependencyInjection;
using Pentra.Application.Execution;

namespace Pentra.Runner;

/// <summary>
/// The privileged execution loop. Recovers interrupted runs on startup, then
/// polls the queue and drives claimed runs through the engine, up to a bounded
/// concurrency. Each run executes in its own DI scope (its own DbContext).
/// </summary>
public sealed class ExecutionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExecutionWorker> _logger;
    private readonly RunnerOptions _options;
    private readonly string _runnerId = $"runner-{Environment.MachineName}-{Guid.NewGuid():N}";

    public ExecutionWorker(IServiceScopeFactory scopeFactory, ILogger<ExecutionWorker> logger, RunnerOptions options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Runner {RunnerId} starting (max concurrency {Max}).", _runnerId, _options.MaxConcurrency);

        await RecoverInterruptedAsync(stoppingToken);

        using var slots = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await slots.WaitAsync(stoppingToken);

                var runId = await ClaimNextAsync(stoppingToken);
                if (runId is null)
                {
                    slots.Release();
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessAsync(runId.Value, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Run {RunId} failed in the worker loop.", runId);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Runner {RunnerId} stopping.", _runnerId);
    }

    private async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExecutionQueue>();
        var recovered = await queue.RecoverInterruptedAsync("Interrupted by runner restart.", ct);
        if (recovered > 0)
        {
            _logger.LogWarning("Recovered {Count} interrupted run(s) as Failed.", recovered);
        }
    }

    private async Task<int?> ClaimNextAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExecutionQueue>();
        return await queue.ClaimNextAsync(_runnerId, ct);
    }

    private async Task ProcessAsync(int runId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IExecutionEngine>();
        _logger.LogInformation("Processing run {RunId}.", runId);
        await engine.ProcessAsync(runId, ct);
    }
}

/// <summary>Runner configuration bound from "Pentra:Runner".</summary>
public sealed class RunnerOptions
{
    public int MaxConcurrency { get; set; } = 2;
    public int PollSeconds { get; set; } = 2;
}
