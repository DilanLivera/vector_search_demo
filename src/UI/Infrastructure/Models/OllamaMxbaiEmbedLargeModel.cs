using System.Text.Json;
using Microsoft.Extensions.AI;

namespace UI.Infrastructure.Models;

/// <summary>
/// Ollama hosted 'mxbai-embed-large' model for generating vector embeddings.
/// </summary>
/// <remarks>
/// This client converts text input into numerical vector representations.
/// </remarks>
public sealed class OllamaMxbaiEmbedLargeModel
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<OllamaMxbaiEmbedLargeModel> _logger;

    public OllamaMxbaiEmbedLargeModel(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<OllamaMxbaiEmbedLargeModel> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Generates a vector embedding for the specified input text.
    /// </summary>
    /// <param name="input">The text content to be converted into a vector embedding.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <c>float[]</c>
    /// representing the generated vector embedding.
    /// </returns>
    public async Task<Result<float[]>> GenerateTextVectorEmbeddingsAsync(string input)
    {
        EmbeddingGenerationOptions options = new()
                                             {
                                                 ModelId = "mxbai-embed-large"
                                             };
        try
        {
            Embedding<float> embedding = await _embeddingGenerator.GenerateAsync(input, options);

            _logger.LogDebug("Embedding: {Embedding}", JsonSerializer.Serialize(embedding));

            return embedding.Vector.ToArray();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate text vector embeddings for '{Input}'", input);

            return Result<float[]>.Failure(exception);
        }
    }

}