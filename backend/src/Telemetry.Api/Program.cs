using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telemetry.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors();
builder.Services.AddHostedService<RedisLiveStream>();
builder.Services.AddSingleton<TelemetryQueryValidator>();
builder.Services.AddSingleton<CouchbaseConnection>();
builder.Services.AddSingleton<CouchbaseTelemetryReader>();
builder.Services.AddSingleton<IConnectionMultiplexer>(services =>
{
    var redisConfiguration = ConfigurationOptions.Parse(
        services.GetRequiredService<IOptions<ApiOptions>>().Value.RedisConnectionString);
    // The API must start (and report unhealthy) while Redis is down.
    redisConfiguration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(redisConfiguration);
});

builder.Services.AddHealthChecks()
    .AddCheck<CouchbaseReadinessCheck>("couchbase", timeout: TimeSpan.FromSeconds(3))
    .AddCheck<RedisReadinessCheck>("redis", timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

var apiOptions = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;
app.UseCors(policy => policy
    .WithOrigins(apiOptions.CorsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials());

app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            dependencies = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        });
    },
});

app.Run();
