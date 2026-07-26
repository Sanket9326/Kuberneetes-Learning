var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("AllowFrontend");

app.MapGet("/healthz", () => Results.Ok("Healthy"));

app.MapGet("/health/live", () => Results.Ok());

app.MapGet("/health/ready", () => Results.Ok());

app.MapGet("/health/startup", () => Results.Ok());

app.MapGet("/api/info", () => Results.Ok(new
{
    hostName = Environment.MachineName,
    environment = app.Environment.EnvironmentName,
    serverTimeUtc = DateTime.UtcNow
}));

app.MapGet("/api/heavy", (int? iterations, int? workers) =>
{
    var n = Math.Clamp(iterations ?? 2_000_000, 1, 50_000_000);
    var parallelism = Math.Clamp(workers ?? Environment.ProcessorCount, 1, Environment.ProcessorCount * 4);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var primesFound = new long[parallelism];

    Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, worker =>
    {
        long count = 0;
        var start = worker * n / parallelism + 2;
        var end = (worker + 1) * n / parallelism + 2;

        for (var candidate = start; candidate < end; candidate++)
        {
            var isPrime = true;
            for (var divisor = 2; (long)divisor * divisor <= candidate; divisor++)
            {
                if (candidate % divisor == 0)
                {
                    isPrime = false;
                    break;
                }
            }

            if (isPrime)
            {
                count++;
            }
        }

        primesFound[worker] = count;
    });

    sw.Stop();

    return Results.Ok(new
    {
        hostName = Environment.MachineName,
        iterations = n,
        workers = parallelism,
        primesFound = primesFound.Sum(),
        elapsedMs = sw.ElapsedMilliseconds
    });
});

app.Run();
