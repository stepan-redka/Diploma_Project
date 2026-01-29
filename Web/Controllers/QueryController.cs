using Microsoft.AspNetCore.Mvc;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Models;

namespace RagWebDemo.Web.Controllers;

/// <summary>
/// API Controller for RAG query operations
/// Follows Single Responsibility Principle - handles only querying
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class QueryController : ControllerBase
{
    private readonly ILogger<QueryController> _logger;
    private readonly IRagService _ragService;

    public QueryController(
        ILogger<QueryController> logger,
        IRagService ragService)
    {
        _logger = logger;
        _ragService = ragService;
    }

    /// <summary>
    /// Query the RAG system with a question
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
}
