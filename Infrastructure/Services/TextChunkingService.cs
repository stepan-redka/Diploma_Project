using System.Text;
using RagWebDemo.Core.Interfaces;

namespace RagWebDemo.Infrastructure.Services;

/// <summary>
/// Service for chunking text into smaller segments for vector storage
/// Follows Single Responsibility Principle - only handles text chunking
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    /// <summary>
    /// Chunks text into smaller overlapping segments for better retrieval
    /// </summary>
    public List<string> ChunkText(string text, int maxChunkSize, int overlap)
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
