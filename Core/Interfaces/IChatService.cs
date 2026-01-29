namespace RagWebDemo.Core.Interfaces;

/// <summary>
/// Interface for chat/LLM service operations
/// Follows Dependency Inversion Principle - Core layer defines the abstraction
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Generates a response from the LLM based on system prompt and user message
    /// </summary>
    /// <param name="systemPrompt">The system prompt defining the assistant's behavior</param>
    /// <param name="userMessage">The user's message/query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated response string</returns>
    Task<string> GenerateResponseAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}
