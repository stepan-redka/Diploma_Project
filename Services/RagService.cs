using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagWebDemo.Models;

// Alias to avoid conflict with Qdrant.Client.Grpc.QueryResponse
using QueryResponse = RagWebDemo.Models.QueryResponse;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0070

namespace RagWebDemo.Services;

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

/// <summary>
/// Info about a stored chunk for display
/// </summary>
public class StoredChunkInfo
{
    public string Id { get; set; } = "";
    public string SourceDocument { get; set; } = "";
    public string ContentPreview { get; set; } = "";
    public int ChunkIndex { get; set; }
}

/// <summary>
/// RAG Service implementing text chunking, embedding generation, vector storage and retrieval
/// Uses Ollama for embeddings, Qdrant for vector storage, and Gemini for answer synthesis
/// </summary>
public class RagService : IRagService
{
    private readonly RagConfiguration _config;
    private readonly ILogger<RagService> _logger;
    private readonly Kernel _kernel;
    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IOllamaChatService _ollamaChatService;

    public RagService(
        IOptions<RagConfiguration> config,
        ILogger<RagService> logger,
        Kernel kernel,
        QdrantClient qdrantClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOllamaChatService ollamaChatService)
    {
        _config = config.Value;
        _logger = logger;
        _kernel = kernel;
        _qdrantClient = qdrantClient;
        _embeddingGenerator = embeddingGenerator;
        _ollamaChatService = ollamaChatService;
    }

    /// <summary>
    /// Ensures the Qdrant collection exists for storing vectors
    /// </summary>
    public async Task<bool> EnsureCollectionExistsAsync()
    {
        try
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            if (!collections.Contains(_config.Qdrant.CollectionName))
            {
                await _qdrantClient.CreateCollectionAsync(
                    collectionName: _config.Qdrant.CollectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)_config.Qdrant.VectorSize,
                        Distance = Distance.Cosine
                    }
                );
                _logger.LogInformation("Created collection: {CollectionName}", _config.Qdrant.CollectionName);
            }
            else
            {
                _logger.LogInformation("Collection already exists: {CollectionName}", _config.Qdrant.CollectionName);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection exists");
            return false;
        }
    }

    /// <summary>
    /// Gets the count of stored documents
    /// </summary>
    public async Task<int> GetDocumentCountAsync()
    {
        try
        {
            var collectionInfo = await _qdrantClient.GetCollectionInfoAsync(_config.Qdrant.CollectionName);
            return (int)collectionInfo.PointsCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Clears all documents from the collection
    /// </summary>
    public async Task<bool> ClearCollectionAsync()
    {
        try
        {
            _logger.LogWarning("Clearing collection: {CollectionName}", _config.Qdrant.CollectionName);
            
            await _qdrantClient.DeleteCollectionAsync(_config.Qdrant.CollectionName);
            await _qdrantClient.CreateCollectionAsync(
                collectionName: _config.Qdrant.CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)_config.Qdrant.VectorSize,
                    Distance = Distance.Cosine
                }
            );
            
            _logger.LogInformation("Collection cleared and recreated");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear collection");
            return false;
        }
    }

    /// <summary>
    /// Gets info about stored chunks for inspection
    /// </summary>
    public async Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500)
    {
        var chunks = new List<StoredChunkInfo>();
        
        try
        {
            var scrollResult = await _qdrantClient.ScrollAsync(
                collectionName: _config.Qdrant.CollectionName,
                limit: (uint)limit,
                payloadSelector: true
            );

            foreach (var point in scrollResult.Result)
            {
                var content = point.Payload.TryGetValue("content", out var c) ? c.StringValue : "";
                var source = point.Payload.TryGetValue("sourceDocument", out var s) ? s.StringValue : "Unknown";
                var chunkIdx = point.Payload.TryGetValue("chunkIndex", out var i) ? (int)i.IntegerValue : 0;

                chunks.Add(new StoredChunkInfo
                {
                    Id = point.Id.Uuid ?? point.Id.Num.ToString(),
                    SourceDocument = source,
                    ContentPreview = content.Length > 150 ? content.Substring(0, 150) + "..." : content,
                    ChunkIndex = chunkIdx
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stored chunks");
        }

        return chunks;
    }

    /// <summary>
    /// Deletes specific chunks by their IDs
    /// </summary>
    public async Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds)
    {
        var idList = chunkIds.ToList();
        if (idList.Count == 0) return 0;

        try
        {
            _logger.LogInformation("Deleting {Count} chunks", idList.Count);

            // Convert string IDs to ulong for Qdrant
            var numericIds = idList
                .Select(id => ulong.TryParse(id, out var num) ? num : (ulong?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            if (numericIds.Count > 0)
            {
                await _qdrantClient.DeleteAsync(
                    collectionName: _config.Qdrant.CollectionName,
                    ids: numericIds,
                    wait: true
                );
            }

            _logger.LogInformation("Successfully deleted {Count} chunks", idList.Count);
            return idList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks");
            return 0;
        }
    }

    /// <summary>
    /// Ingests a document by chunking it and storing embeddings in Qdrant
    /// </summary>
    public async Task<IngestResponse> IngestDocumentAsync(string content, string documentName)
    {
        try
        {
            _logger.LogInformation("Starting ingestion of document: {DocumentName}", documentName);

            // Ensure collection exists
            await EnsureCollectionExistsAsync();

            // Step 1: Chunk the text
            var chunks = ChunkText(content, _config.Chunking.MaxChunkSize, _config.Chunking.ChunkOverlap);
            _logger.LogInformation("Created {ChunkCount} chunks from document", chunks.Count);

            if (chunks.Count == 0)
            {
                return new IngestResponse
                {
                    Success = false,
                    Message = "No valid chunks could be created from the document",
                    ChunksCreated = 0
                };
            }

            // Step 2: Generate embeddings for all chunks
            var embeddings = await _embeddingGenerator.GenerateAsync(chunks);
            _logger.LogInformation("Generated {EmbeddingCount} embeddings", embeddings.Count);

            // Step 3: Create points for Qdrant
            var points = new List<PointStruct>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var pointId = Guid.NewGuid();
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId.ToString() },
                    Vectors = embeddings[i].Vector.ToArray(),
                    Payload =
                    {
                        ["content"] = chunks[i],
                        ["sourceDocument"] = documentName,
                        ["chunkIndex"] = i,
                        ["createdAt"] = DateTime.UtcNow.ToString("o")
                    }
                };
                points.Add(point);
            }

            // Step 4: Upsert points to Qdrant
            await _qdrantClient.UpsertAsync(
                collectionName: _config.Qdrant.CollectionName,
                points: points
            );

            _logger.LogInformation("Successfully ingested document: {DocumentName}", documentName);

            return new IngestResponse
            {
                Success = true,
                Message = $"Successfully ingested '{documentName}' into {chunks.Count} chunks",
                ChunksCreated = chunks.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest document: {DocumentName}", documentName);
            return new IngestResponse
            {
                Success = false,
                Message = $"Failed to ingest document: {ex.Message}",
                ChunksCreated = 0
            };
        }
    }

    /// <summary>
    /// Queries the RAG system - retrieves relevant chunks and generates answer using Gemini
    /// </summary>
    public async Task<QueryResponse> QueryAsync(string question, int topK = 3)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing query: {Question}", question);

            // Step 1: Generate embedding for the question
            var questionEmbeddings = await _embeddingGenerator.GenerateAsync([question]);
            var questionEmbedding = questionEmbeddings.First();

            // Step 2: Search in Qdrant
            var searchResults = await _qdrantClient.SearchAsync(
                collectionName: _config.Qdrant.CollectionName,
                vector: questionEmbedding.Vector.ToArray(),
                limit: (ulong)topK,
                scoreThreshold: 0.3f
            );

            // Step 3: Extract context from results
            var retrievedContexts = new List<RetrievedContext>();
            foreach (var result in searchResults)
            {
                var content = result.Payload.TryGetValue("content", out var contentValue) 
                    ? contentValue.StringValue 
                    : "";
                var sourceDoc = result.Payload.TryGetValue("sourceDocument", out var sourceValue) 
                    ? sourceValue.StringValue 
                    : "Unknown";

                retrievedContexts.Add(new RetrievedContext
                {
                    Content = content,
                    SourceDocument = sourceDoc,
                    Score = result.Score
                });
            }

            _logger.LogInformation("Retrieved {Count} relevant chunks", retrievedContexts.Count);

            // Step 4: Generate answer using Gemini
            string answer;
            if (retrievedContexts.Count == 0)
            {
                answer = "I couldn't find any relevant information in the knowledge base to answer your question. Please try rephrasing or ensure relevant documents have been uploaded.";
            }
            else
            {
                answer = await GenerateAnswerWithOllamaAsync(question, retrievedContexts);
            }

            stopwatch.Stop();

            return new QueryResponse
            {
                Answer = answer,
                Sources = retrievedContexts,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to process query: {Question}", question);

            return new QueryResponse
            {
                Answer = $"An error occurred while processing your query: {ex.Message}",
                Sources = new List<RetrievedContext>(),
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Generates an answer using Google Gemini based on retrieved context
    /// </summary>
    private async Task<string> GenerateAnswerWithGeminiAsync(string question, List<RetrievedContext> contexts)
    {
        // Build context string from retrieved chunks
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < contexts.Count; i++)
        {
            contextBuilder.AppendLine($"[Source {i + 1}]: {contexts[i].Content}");
            contextBuilder.AppendLine();
        }

        // Create RAG prompt
        var prompt = $"""
            You are a helpful assistant that answers questions based on the provided context.
            Use ONLY the information from the context below to answer the question.
            If the context doesn't contain enough information to answer, say so clearly.
            Be concise but thorough in your response.

            CONTEXT:
            {contextBuilder}

            QUESTION: {question}

            ANSWER:
            """;

        // Retry with exponential backoff for rate limiting
        const int maxRetries = 5;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Use Semantic Kernel to invoke Gemini
                var result = await _kernel.InvokePromptAsync(prompt);
                return result.GetValue<string>() ?? "Unable to generate response.";
            }
            catch (Exception ex) when (ex.Message.Contains("429") || ex.InnerException?.Message.Contains("429") == true)
            {
                if (attempt < maxRetries - 1)
                {
                    var delayMs = (int)Math.Pow(2, attempt + 2) * 1000; // 4s, 8s, 16s, 32s
                    _logger.LogWarning("Rate limited by Gemini API. Retrying in {Delay}ms (attempt {Attempt}/{Max})", 
                        delayMs, attempt + 1, maxRetries);
                    await Task.Delay(delayMs);
                }
                else
                {
                    _logger.LogError(ex, "Rate limit exceeded after {MaxRetries} retries", maxRetries);
                    return "⚠️ The Gemini API is rate-limited. Please wait 30-60 seconds and try again. Free tier allows ~15 requests/minute.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate answer with Gemini");
                throw;
            }
        }
        
        return "Unable to generate response after retries.";
    }

    /// <summary>
    /// Generates an answer using Ollama based on retrieved context
    /// </summary>
    private async Task<string> GenerateAnswerWithOllamaAsync(string question, List<RetrievedContext> contexts)
    {
        // Build context string from retrieved chunks
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < contexts.Count; i++)
        {
            contextBuilder.AppendLine($"[Source {i + 1}]: {contexts[i].Content}");
            contextBuilder.AppendLine();
        }

        var systemPrompt = @"You are a helpful assistant that answers questions based on provided context.
Use ONLY the information from the context to answer questions.
If the context doesn't contain enough information, say so clearly.
Be concise but thorough in your response.";

        var userMessage = $@"CONTEXT:
{contextBuilder}

QUESTION: {question}

ANSWER:";

        try
        {
            var answer = await _ollamaChatService.GenerateResponseAsync(systemPrompt, userMessage);
            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate answer with Ollama");
            return "An error occurred while generating the answer. Please try again.";
        }
    }

    /// <summary>
    /// Chunks text into smaller overlapping segments for better retrieval
    /// </summary>
    private List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        var chunks = new List<string>();
        
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // Normalize whitespace
        text = string.Join(" ", text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

        // Split by sentences first for more natural chunks
        var sentences = SplitIntoSentences(text);
        var currentChunk = new StringBuilder();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            // If adding this sentence would exceed max size, save current chunk
            if (currentLength + sentence.Length > maxChunkSize && currentLength > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                
                // Start new chunk with overlap from previous content
                var overlapText = GetOverlapText(currentChunk.ToString(), overlap);
                currentChunk.Clear();
                currentChunk.Append(overlapText);
                currentLength = overlapText.Length;
            }

            currentChunk.Append(sentence).Append(" ");
            currentLength += sentence.Length + 1;
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        // Filter out very small chunks
        chunks = chunks.Where(c => c.Length >= 50).ToList();

        return chunks;
    }

    /// <summary>
    /// Splits text into sentences using common delimiters
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            current.Append(text[i]);

            // Check for sentence boundaries
            if (text[i] == '.' || text[i] == '!' || text[i] == '?')
            {
                // Look ahead to verify it's actually end of sentence
                if (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]) || char.IsUpper(text[i + 1]))
                {
                    sentences.Add(current.ToString().Trim());
                    current.Clear();
                }
            }
        }

        // Add any remaining text
        if (current.Length > 0)
        {
            sentences.Add(current.ToString().Trim());
        }

        return sentences;
    }

    /// <summary>
    /// Gets the last 'overlap' characters from text for chunk overlap
    /// </summary>
    private string GetOverlapText(string text, int overlap)
    {
        if (string.IsNullOrEmpty(text) || overlap <= 0)
            return string.Empty;

        text = text.Trim();
        if (text.Length <= overlap)
            return text;

        // Try to find a word boundary for cleaner overlap
        var startIndex = text.Length - overlap;
        var spaceIndex = text.IndexOf(' ', startIndex);
        
        if (spaceIndex > startIndex && spaceIndex < text.Length)
        {
            return text.Substring(spaceIndex + 1);
        }

        return text.Substring(startIndex);
    }
}