using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Serilog.Events;

namespace HttpServerToGraylog;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// see doc at https://go2docs.graylog.org/5-0/getting_in_log_data/gelf.html
/// </remarks>
public sealed class GelfHttpClient(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<GelfHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };
    private readonly HttpClient _client = httpClientFactory.CreateClient();
    private readonly string? _gelfUrl = configuration["Graylog:GelfUrl"];
    
    
    /// <summary>
    /// Send array of events to Graylog
    /// </summary>
    public async Task SendAsync(string host, LogEvent[]? events, CancellationToken cancellationToken = default)
    {
        if (events == null || events.Length == 0) return;
        
        var list = events.Select(x => ToJsonString(host, x));
        var jsonMessages = string.Join('\n', list);
        await SendAsync(jsonMessages, cancellationToken);
    }

    private static string ToJsonString(string host, LogEvent e)
    {
        // log event level to graylog level
        var level = e.Level switch
        {
            LogEventLevel.Verbose => 7,
            LogEventLevel.Debug => 7,
            LogEventLevel.Information => 6,
            LogEventLevel.Warning => 4,
            LogEventLevel.Error => 3,
            LogEventLevel.Fatal => 2,
            _ => 6
        };
        
        var data = new Dictionary<string, object>
        {

            ["version"] = "1.1",
            ["host"] = host,
            ["short_message"] = e.RenderedMessage.ReplaceLineEndings("\n"),
            //["full_message"] = "",
            ["timestamp"] = (double)(e.Timestamp.ToUnixTimeMilliseconds() / 1000.0),
            ["level"] = level,
        };

        if (e.Properties != null)
        {
            foreach (var (key, value) in e.Properties)
            {
                data[$"_{key}"] = value;
            }
        }
            
        return JsonSerializer.Serialize(data, JsonSerializerOptions);
    }
    
    private async Task SendAsync(string jsonMessages, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Sending: {json}", jsonMessages);
        
        var response = await _client.PostAsync(
            _gelfUrl, 
            new StringContent(jsonMessages, Encoding.UTF8, MediaTypeNames.Application.Json), 
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
    }
}

/*
 {
"version": "1.1",
"host": "example.org",
"short_message": "A short message that helps you identify what is going on",
"full_message": "Backtrace here\n\nmore stuff",
"timestamp": 1385053862.3072,
"level": 1,
"_user_id": 9001,
"_some_info": "foo",
"_some_env_var": "bar"
}
*/