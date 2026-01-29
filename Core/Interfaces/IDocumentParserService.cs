using RagWebDemo.Core.Enums;
using RagWebDemo.Core.Entities;

namespace RagWebDemo.Core.Interfaces;

/// <summary>
/// Interface for document parsing service
/// </summary>
public interface IDocumentParserService
{
    Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName);
    DocumentType GetDocumentType(string fileName);
    bool IsSupported(string fileName);
}
