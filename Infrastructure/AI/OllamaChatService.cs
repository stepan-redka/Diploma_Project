using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagWebDemo.Core.Interfaces;

namespace RagWebDemo.Infrastructure.AI;

// Keep the old interface for backward compatibility but mark as obsolete
[Obsolete("Use IChatService from Core.Interfaces instead")]
public interface IOllamaChatService : IChatService { }

/// <summary>
/// Ollama-based chat service implementation
/// Follows Dependency Inversion Principle - implements abstraction from Core layer
/// </summary>
#pragma warning disable CS0618 // Suppress obsolete warning for backward compatibility
public class OllamaChatService : IChatService, IOllamaChatService
#pragma warning restore CS0618
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly ILogger<OllamaChatService> _logger;

    public OllamaChatService(HttpClient httpClient, string modelName, ILogger<OllamaChatService> logger)
    {
        _httpClient = httpClient;
        _modelName = modelName;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new OllamaChatRequest
            {
                Model = _modelName,
                Messages = new[]
                {
                    new OllamaMessage { Role = "system", Content = systemPrompt },
                    new OllamaMessage { Role = "user", Content = userMessage }
                },
                Stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/chat", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);

            return result?.Message?.Content ?? "No response generated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate response from Ollama");
            throw;
        }
    }

    private class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public OllamaMessage[] Messages { get; set; } = Array.Empty<OllamaMessage>();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }
}
