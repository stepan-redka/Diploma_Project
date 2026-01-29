using Microsoft.AspNetCore.Mvc;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Models;

namespace RagWebDemo.Web.Controllers;

/// <summary>
/// API Controller for document ingestion operations
/// Follows Single Responsibility Principle - handles only document ingestion
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class DocumentsController : ControllerBase
{
    private readonly ILogger<DocumentsController> _logger;
    private readonly IRagService _ragService;
    private readonly IDocumentParserService _documentParser;

    public DocumentsController(
        ILogger<DocumentsController> logger,
        IRagService ragService,
        IDocumentParserService documentParser)
    {
        _logger = logger;
        _ragService = ragService;
        _documentParser = documentParser;
    }

    /// <summary>
    /// Ingest a document from raw text content
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required" });
        }

        if (string.IsNullOrWhiteSpace(request.DocumentName))
        {
            request.DocumentName = $"Document_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        var result = await _ragService.IngestDocumentAsync(request.Content, request.DocumentName);
        
        if (result.Success)
        {
            return Ok(result);
        }
        
        return StatusCode(500, result);
    }

    /// <summary>
    /// Upload and ingest a file (PDF, DOCX, TXT, etc.)
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50MB limit
    public async Task<IActionResult> UploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        // Check if file type is supported
        if (!_documentParser.IsSupported(file.FileName))
        {
            return BadRequest(new { 
                error = $"File type not supported: {Path.GetExtension(file.FileName)}",
                supportedTypes = new[] { ".txt", ".pdf", ".docx", ".md", ".html", ".csv", ".json", ".xml" }
            });
        }

        // Parse the document
        using var stream = file.OpenReadStream();
        var parsed = await _documentParser.ParseAsync(stream, file.FileName);

        if (!parsed.Success)
        {
            return BadRequest(new { error = $"Failed to parse document: {parsed.ErrorMessage}" });
        }

        if (string.IsNullOrWhiteSpace(parsed.Content))
        {
            return BadRequest(new { error = "Document appears to be empty or could not extract text" });
        }

        // Ingest the parsed content
        var result = await _ragService.IngestDocumentAsync(parsed.Content, file.FileName);
        
        if (result.Success)
        {
            return Ok(new
            {
                result.Success,
                result.Message,
                result.ChunksCreated,
                fileName = file.FileName,
                fileSize = file.Length,
                characterCount = parsed.CharacterCount,
                documentType = parsed.Type.ToString()
            });
        }
        
        return StatusCode(500, result);
    }
}
