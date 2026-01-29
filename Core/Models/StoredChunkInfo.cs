namespace RagWebDemo.Core.Models;

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
