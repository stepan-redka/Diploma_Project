using RagWebDemo.Core.Models;
using RagWebDemo.Infrastructure.Services;

namespace RagWebDemo.Core.Interfaces;

/// <summary>
/// Interface for RAG operations - enables testing and future extensibility
/// </summary>
public interface IRagService
{
    Task<IngestResponse> IngestDocumentAsync(string content, string documentName);
    Task<QueryResponse> QueryAsync(string question, int topK = 3);
    Task<bool> EnsureCollectionExistsAsync();
    Task<int> GetDocumentCountAsync();
    Task<bool> ClearCollectionAsync();
    Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500);
    Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds);
}
