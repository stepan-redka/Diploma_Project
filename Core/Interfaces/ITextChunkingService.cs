namespace RagWebDemo.Core.Interfaces;

/// <summary>
/// Interface for text chunking operations - follows Single Responsibility Principle
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// Chunks text into smaller overlapping segments for better retrieval
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="maxChunkSize">Maximum size of each chunk</param>
    /// <param name="overlap">Number of characters to overlap between chunks</param>
    /// <returns>List of text chunks</returns>
    List<string> ChunkText(string text, int maxChunkSize, int overlap);
}
