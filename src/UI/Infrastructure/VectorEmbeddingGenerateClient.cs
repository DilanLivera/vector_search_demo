using Qdrant.Client.Grpc;

namespace UI.Infrastructure;

public sealed class VectorEmbeddingGenerateClient
{
    private readonly HttpClient _client;

    public VectorEmbeddingGenerateClient(HttpClient client) => _client = client;

    public async Task<float[]> GenerateVectorEmbeddings(string input)
    {
        VectorEmbeddingsGenerateRequest request = new(input);
        HttpResponseMessage responseMessage = await _client.PostAsJsonAsync(requestUri: "http://localhost:11434/api/embed", request);

        responseMessage.EnsureSuccessStatusCode();

        VectorEmbeddingsGenerateResponse embeddings = await responseMessage.Content.ReadFromJsonAsync<VectorEmbeddingsGenerateResponse>() ?? throw new InvalidOperationException($"Failed to deserialize generate vector embeddings response to {nameof(List<PointStruct>)} type");

        return embeddings.Embeddings[0]; // is this because the input is text
    }

    private record VectorEmbeddingsGenerateRequest(string Input, string Model = "mxbai-embed-large");

    private record VectorEmbeddingsGenerateResponse(string Model, float[][] Embeddings, int TotalDuration, int LoadDuration, int PromptEvalCount);
}