using RagWebDemo.Core.Models;

namespace RagWebDemo.Core.Interfaces;

/// <summary>
/// Interface for answer generation - follows Single Responsibility Principle
/// </summary>
public interface IAnswerGenerationService
{
    /// <summary>
    /// Generates an answer based on retrieved context
    /// </summary>
    /// <param name="question">The user's question</param>
    /// <param name="contexts">Retrieved context from vector search</param>
    /// <returns>Generated answer string</returns>
    Task<string> GenerateAnswerAsync(string question, List<RetrievedContext> contexts);
}
