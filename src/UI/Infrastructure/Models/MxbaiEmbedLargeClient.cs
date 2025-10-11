namespace UI.Infrastructure.Models;

/// <summary>
/// A client for generating vector embeddings using locally hosted 'mxbai-embed-large' model.
/// </summary>
/// <remarks>
/// This client converts text input into numerical vector representations.
/// </remarks>
public sealed class MxbaiEmbedLargeClient
{
    private readonly HttpClient _client;

    public MxbaiEmbedLargeClient(HttpClient client) => _client = client;

    /// <summary>
    /// Generates a vector embedding for the specified input text.
    /// </summary>
    /// <param name="input">The text content to be converted into a vector embedding.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a <c>float[]</c>
    /// representing the generated vector embedding.
    /// </returns>
    /// <remarks>
    /// This method sends a single input string to the embedding API. The API can process batches,
    /// so this implementation retrieves only the first embedding from the response array,
    /// which corresponds to the single input provided.
    /// </remarks>
    /// <exception cref="HttpRequestException">Thrown if the API request does not return a success status code.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the response from the API cannot be deserialized correctly.</exception>
    public async Task<float[]> GenerateTextVectorEmbeddingsAsync(string input)
    {
        VectorEmbeddingsGenerateRequest request = new(input);
        HttpResponseMessage responseMessage = await _client.PostAsJsonAsync(requestUri: "http://localhost:11434/api/embed", request);

        responseMessage.EnsureSuccessStatusCode();

        VectorEmbeddingsGenerateResponse embeddings = await responseMessage.Content.ReadFromJsonAsync<VectorEmbeddingsGenerateResponse>() ?? throw new InvalidOperationException($"Failed to deserialize generate vector embeddings response to {nameof(VectorEmbeddingsGenerateResponse)} type");

        // This returns the first element because the API returns an array of embeddings (float[][]),
        // one for each input string. Since we only send one string, we take the first result.
        return embeddings.Embeddings[0];
    }

    /// <summary>
    /// Represents the request payload for generating vector embeddings.
    /// </summary>
    /// <param name="Input">The text to be embedded.</param>
    /// <param name="Model">The name of the embedding model to use.</param>
    private record VectorEmbeddingsGenerateRequest(string Input, string Model = "mxbai-embed-large");

    /// <summary>
    /// Represents the response received from the vector embedding generation API.
    /// </summary>
    /// <param name="Model">The model that generated the embeddings.</param>
    /// <param name="Embeddings">A list of generated embeddings. Each embedding is a float array.</param>
    /// <param name="TotalDuration">The total duration of the generation process.</param>
    /// <param name="LoadDuration">The duration it took to load the model.</param>
    /// <param name="PromptEvalCount">The number of tokens in the prompt.</param>
    private record VectorEmbeddingsGenerateResponse(
        string Model,
        float[][] Embeddings,
        int TotalDuration,
        int LoadDuration,
        int PromptEvalCount);
}