using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;

namespace UI.Infrastructure;


// depends on the Azure.AI.Inference(pre-release) packagesssssss
// https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-models/how-to/use-image-embeddings?pivots=programming-language-csharp
public sealed class ImageVectorEmbeddingGenerateClient
{
    private readonly string _imageEmbeddingClientBaseUrl;
    private readonly ILogger<ImageVectorEmbeddingGenerateClient> _logger;
    private readonly ImageEmbeddingsClient _client;

    public ImageVectorEmbeddingGenerateClient(
        IConfiguration configuration,
        ILogger<ImageVectorEmbeddingGenerateClient> logger)
    {
        _logger = logger;

        string azureInferenceCredential = configuration.GetValue<string>(key: "AzureAiInference:AzureKeyCredential") ?? throw new InvalidOperationException("'AzureInference:Credential' configuration is not set");
        _imageEmbeddingClientBaseUrl = configuration.GetValue<string>(key: "AzureAiInference:Endpoint") ?? throw new InvalidOperationException("'AzureInference:BaseUrl' configuration is not set");

        _client = new ImageEmbeddingsClient(new Uri(_imageEmbeddingClientBaseUrl),
                                            new AzureKeyCredential(azureInferenceCredential));
    }

    public async Task<float[]> GenerateVectorEmbeddings(string imageFilePath, string imageFormat)
    {
        List<ImageEmbeddingInput> input = [ImageEmbeddingInput.Load(imageFilePath, imageFormat)];

        ImageEmbeddingsOptions requestOptions = new(input)
                                                {
                                                    Model = "Cohere-embed-v3-english"
                                                };

        try
        {
            Response<EmbeddingsResult> response = await _client.EmbedAsync(requestOptions);

            _logger.LogDebug("Embedding {EmbeddingsResponse}, BaseUrl: {AzureInferenceBaseUrl}, Path: {AzureInferenceEmbeddingPath}",
                             JsonSerializer.Serialize(response),
                             _imageEmbeddingClientBaseUrl,
                             "/images/embeddings");

            float[][] embeddings = response.Value
                                           .Data
                                           .Select(i => i.Embedding.ToObjectFromJson<float[]>() ?? throw new InvalidOperationException("Failed to deserialize embedding item."))
                                           .ToArray();

            Debug.Assert(embeddings.Length == 1, message: "Embedding list must contain only one item.");

            return embeddings.First(); // todo: must return a result type
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate embedding for image");

            throw; // todo: must return an error result instead of throwing
        }
    }
}