using Microsoft.AspNetCore.Mvc;
using RagWebDemo.Models;
using RagWebDemo.Services;

namespace RagWebDemo.Controllers;

/// <summary>
/// Controller for database management operations
/// </summary>
public class DatabaseController : Controller
{
    private readonly ILogger<DatabaseController> _logger;
    private readonly IRagService _ragService;

    public DatabaseController(ILogger<DatabaseController> logger, IRagService ragService)
    {
        _logger = logger;
        _ragService = ragService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var documentCount = await _ragService.GetDocumentCountAsync();
        var collectionReady = await _ragService.EnsureCollectionExistsAsync();

        return Ok(new
        {
            status = collectionReady ? "ready" : "error",
            documentCount = documentCount
        });
    }

    [HttpGet]
    public async Task<IActionResult> ViewChunks(int limit = 500)
    {
        var chunks = await _ragService.GetStoredChunksAsync(limit);
        return Ok(new { count = chunks.Count, chunks });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteChunks([FromBody] DeleteChunksRequest request)
    {
        if (request.ChunkIds == null || request.ChunkIds.Length == 0)
            return BadRequest(new { success = false, message = "No chunk IDs provided" });

        var deletedCount = await _ragService.DeleteChunksAsync(request.ChunkIds);
        
        return Ok(new
        {
            success = deletedCount > 0,
            deletedCount,
            message = deletedCount > 0 ? $"Deleted {deletedCount} chunk(s)" : "Failed to delete"
        });
    }

    [HttpPost]
    public async Task<IActionResult> ClearDatabase()
    {
        var success = await _ragService.ClearCollectionAsync();
        return Ok(new { success, message = success ? "Database cleared" : "Failed to clear database" });
    }
}
