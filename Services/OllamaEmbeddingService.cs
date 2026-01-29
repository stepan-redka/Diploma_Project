using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace RagWebDemo.Services;

public class OllamaEmbeddingService : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    public OllamaEmbeddingService(HttpClient httpClient, string modelName)
    {
        _httpClient = httpClient;
        _modelName = modelName;
    }

    public EmbeddingGeneratorMetadata Metadata => new("ollama", _httpClient.BaseAddress, _modelName);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, 
        EmbeddingGenerationOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<Embedding<float>>();
        
        foreach (var value in values)
        {
            var request = new
            {
                model = _modelName,
                input = value
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/embed", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson);

            if (result?.Embeddings != null && result.Embeddings.Length > 0)
            {
                embeddings.Add(new Embedding<float>(result.Embeddings[0]));
            }
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public TService? GetService<TService>(object? key = null) where TService : class => null;

    public void Dispose() { }

    private class OllamaEmbeddingResponse
    {
        public float[][]? Embeddings { get; set; }
    }
}
