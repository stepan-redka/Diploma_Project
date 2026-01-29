using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Models;

namespace RagWebDemo.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IRagService _ragService;
    private readonly IDocumentParserService _documentParser;

    public HomeController(
        ILogger<HomeController> logger, 
        IRagService ragService,
        IDocumentParserService documentParser)
    {
        _logger = logger;
        _ragService = ragService;
        _documentParser = documentParser;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// API endpoint to ingest a document into the RAG system
    /// </summary>
    [HttpPost]
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
    /// API endpoint to upload and ingest a file (PDF, DOCX, TXT, etc.)
    /// </summary>
    [HttpPost]
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

    /// <summary>
    /// API endpoint to query the RAG system
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Query([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "Question is required" });
        }

        var result = await _ragService.QueryAsync(request.Question, request.TopK);
        return Ok(result);
    }

    /// <summary>
    /// API endpoint to get system status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var documentCount = await _ragService.GetDocumentCountAsync();
        var collectionReady = await _ragService.EnsureCollectionExistsAsync();

        return Ok(new
        {
            status = collectionReady ? "ready" : "error",
            documentCount = documentCount,
            timestamp = DateTime.UtcNow
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
