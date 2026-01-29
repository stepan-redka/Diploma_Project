using Microsoft.AspNetCore.Mvc;
using RagWebDemo.Core.Interfaces;

namespace RagWebDemo.Web.Controllers;

/// <summary>
/// API Controller for system status operations
/// Follows Single Responsibility Principle - handles only status checks
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase
{
    private readonly ILogger<StatusController> _logger;
    private readonly IRagService _ragService;

    public StatusController(
        ILogger<StatusController> logger,
        IRagService ragService)
    {
        _logger = logger;
        _ragService = ragService;
    }

    /// <summary>
    /// Get the current system status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var documentCount = await _ragService.GetDocumentCountAsync();
        var collectionReady = await _ragService.EnsureCollectionExistsAsync();

        return Ok(new
        {
            status = collectionReady ? "ready" : "error",
            documentCount,
            timestamp = DateTime.UtcNow
        });
    }
}
