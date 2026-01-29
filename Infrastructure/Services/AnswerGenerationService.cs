using System.Text;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Models;

namespace RagWebDemo.Infrastructure.Services;

/// <summary>
/// Service for generating answers using LLM based on retrieved context
/// Follows Single Responsibility Principle - only handles answer generation
/// Follows Dependency Inversion Principle - depends on IChatService abstraction
/// </summary>
public class AnswerGenerationService : IAnswerGenerationService
{
    private readonly IChatService _chatService;
    private readonly ILogger<AnswerGenerationService> _logger;

    public AnswerGenerationService(
        IChatService chatService,
        ILogger<AnswerGenerationService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Generates an answer using Ollama based on retrieved context
    /// </summary>
    public async Task<string> GenerateAnswerAsync(string question, List<RetrievedContext> contexts)
    {
        if (contexts.Count == 0)
        {
            return "I couldn't find any relevant information in the knowledge base to answer your question. Please try rephrasing or ensure relevant documents have been uploaded.";
        }

        // Build context string from retrieved chunks
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < contexts.Count; i++)
        {
            contextBuilder.AppendLine($"[Source {i + 1}]: {contexts[i].Content}");
            contextBuilder.AppendLine();
        }

        var systemPrompt = @"You are a helpful assistant that answers questions based on provided context.
Use ONLY the information from the context to answer questions.
If the context doesn't contain enough information, say so clearly.
Be concise but thorough in your response.";

        var userMessage = $@"CONTEXT:
{contextBuilder}

QUESTION: {question}

ANSWER:";

        try
        {
            var answer = await _chatService.GenerateResponseAsync(systemPrompt, userMessage);
            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate answer");
            return "An error occurred while generating the answer. Please try again.";
        }
    }
}
