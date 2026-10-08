using Docker.DotNet;
using Pentra.Application;
using Pentra.Domain.Abstractions;
using Pentra.Infrastructure;
using Pentra.Infrastructure.Persistence;
using Pentra.Runner;
using Pentra.Runner.Docker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Runner options from configuration ("Pentra:Runner").
var runnerOptions = builder.Configuration.GetSection("Pentra:Runner").Get<RunnerOptions>() ?? new RunnerOptions();
builder.Services.AddSingleton(runnerOptions);

// Docker access lives ONLY in this process. The Docker client connects to the
// local Engine via the socket mounted into this container.
builder.Services.AddSingleton<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
builder.Services.AddSingleton<IToolRunner, DockerToolRunner>();

builder.Services.AddHostedService<ExecutionWorker>();

var host = builder.Build();

// Ensure the schema exists (EF serializes concurrent migrations via its lock table).
await DatabaseInitializer.InitializeAsync(host.Services);

host.Run();
