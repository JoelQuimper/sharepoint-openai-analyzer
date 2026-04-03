using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.RegularExpressions;

public class LoggingPolicy : PipelinePolicy
{
    private static void ProcessMessage(PipelineMessage message)
    {
        if (message.Request is not null && message.Response is null)
        {
            Console.WriteLine($"{message?.Request?.Method} URI: {message?.Request?.Uri}");
            Console.WriteLine($"--- New request ---");
            IEnumerable<string> headerPairs = message?.Request?.Headers?.Select(header => $"\n    {header.Key}={(header.Key.ToLower().Contains("auth") ? "***" : header.Value)}");
            string headers = string.Join("", headerPairs);
            Console.WriteLine($"Request headers:{headers}");
            if (message.Request?.Content != null)
            {
                string contentType = "Unknown Content Type";
                if (message.Request.Headers?.TryGetValue("Content-Type", out contentType) == true
                    && contentType == "application/json")
                {
                    using MemoryStream stream = new();
                    message.Request.Content.WriteTo(stream, default);
                    stream.Position = 0;
                    using StreamReader reader = new(stream);
                    string requestDump = reader.ReadToEnd();
                    stream.Position = 0;
                    requestDump = Regex.Replace(requestDump, @"""data"":[\\w\\r\\n]*""[^""]*""", @"""data"":""...""");
                    // Make sure JSON string is properly formatted.
                    JsonSerializerOptions jsonOptions = new()
                    {
                        WriteIndented = true,
                    };
                    JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(requestDump);
                    Console.WriteLine("--- Begin request content ---");
                    Console.WriteLine(JsonSerializer.Serialize(jsonElement, jsonOptions));
                    Console.WriteLine("--- End request content ---");
                }
                else
                {
                    string length = message.Request.Content.TryComputeLength(out long numberLength)
                        ? $"{numberLength} bytes"
                        : "unknown length";
                    Console.WriteLine($"<< Non-JSON content: {contentType} >> {length}");
                }
            }
        }
        if (message.Response != null)
        {
            IEnumerable<string> headerPairs = message?.Response?.Headers?.Select(header => $"\n    {header.Key}={(header.Key.ToLower().Contains("auth") ? "***" : header.Value)}");
            string headers = string.Join("", headerPairs);
            Console.WriteLine($"Response headers:{headers}");
            if (message.BufferResponse)
            {
                message.Response.BufferContent();
                Console.WriteLine("--- Begin response content ---");
                Console.WriteLine(message.Response.Content?.ToString());
                Console.WriteLine("--- End of response content ---");
            }
            else
            {
                Console.WriteLine("--- Response (unbuffered, content not rendered) ---");
            }
        }
    }

    public LoggingPolicy(){}

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ProcessMessage(message); // for request
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            ProcessNext(message, pipeline, currentIndex);
        }
        finally
        {
            Console.WriteLine($"Response time {stopwatch.Elapsed.TotalMilliseconds} ms");
        }
        ProcessMessage(message); // for response
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ProcessMessage(message); // for request
        DateTime start = DateTime.Now;
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await ProcessNextAsync(message, pipeline, currentIndex);
        }
        finally
        {
            Console.WriteLine($"Response time {stopwatch.Elapsed.TotalMilliseconds} ms");
        }
        ProcessMessage(message); // for response
    }
}