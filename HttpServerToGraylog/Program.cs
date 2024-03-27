using System.Text.Json.Serialization;
using HttpServerToGraylog;
using Microsoft.AspNetCore.Mvc;
using Serilog.Events;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// add console log with format
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yy-MM-dd HH:mm:ss ";
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<GelfHttpClient>();

var app = builder.Build();

var idempotencyKeys = new HashSet<string>();

var serilogs = app.MapGroup("/serilogs");
serilogs.MapPost("/{key}",
    handler: async ([FromRoute] string key,
        [FromBody] LogEvent[]? events,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] IConfiguration configuration,
        [FromServices] GelfHttpClient gelfClient,
        [FromServices] ILogger<Program> logger) =>
    {
        try
        {
            if (events == null || events.Length == 0) return Results.BadRequest();

            // check idempotency
            if (!string.IsNullOrEmpty(idempotencyKey) && !idempotencyKeys.Add(idempotencyKey))
            {
                logger.LogWarning("Idempotency key {key} already exists", idempotencyKey);
                return Results.Conflict();
            }
            
            // check key
            var val = configuration[$"Keys:{key}"];
            if (string.IsNullOrEmpty(val)) return Results.Unauthorized();
        
            await gelfClient.SendAsync(val, events);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send log events");
        }
        return Results.Ok();
    });


app.Run();


[JsonSerializable(typeof(LogEvent[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>
/// Represents a log event.
/// </summary>
public record LogEvent (
    DateTimeOffset Timestamp, 
    LogEventLevel Level,
    string MessageTemplate, 
    string RenderedMessage, 
    Dictionary<string,object>? Properties);