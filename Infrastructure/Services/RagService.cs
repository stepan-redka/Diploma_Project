using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Models;

// Alias to avoid conflict with Qdrant.Client.Grpc.QueryResponse
using QueryResponse = RagWebDemo.Core.Models.QueryResponse;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0070

namespace RagWebDemo.Infrastructure.Services;

/// <summary>
/// RAG Service implementing embedding generation, vector storage and retrieval
/// Follows Single Responsibility Principle - delegates chunking and answer generation to dedicated services
/// Follows Dependency Inversion Principle - depends on abstractions (interfaces) not concretions
/// </summary>
public class RagService : IRagService
{
    private readonly RagConfiguration _config;
    private readonly ILogger<RagService> _logger;
    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ITextChunkingService _textChunkingService;
    private readonly IAnswerGenerationService _answerGenerationService;

    public RagService(
        IOptions<RagConfiguration> config,
        ILogger<RagService> logger,
        QdrantClient qdrantClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ITextChunkingService textChunkingService,
        IAnswerGenerationService answerGenerationService)
    {
        _config = config.Value;
        _logger = logger;
        _qdrantClient = qdrantClient;
        _embeddingGenerator = embeddingGenerator;
        _textChunkingService = textChunkingService;
        _answerGenerationService = answerGenerationService;
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

            // Step 1: Chunk the text using dedicated service
            var chunks = _textChunkingService.ChunkText(content, _config.Chunking.MaxChunkSize, _config.Chunking.ChunkOverlap);
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

            // Step 4: Generate answer using dedicated service
            var answer = await _answerGenerationService.GenerateAnswerAsync(question, retrievedContexts);

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
}