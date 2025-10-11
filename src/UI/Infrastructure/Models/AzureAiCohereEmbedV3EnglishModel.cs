using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;

namespace UI.Infrastructure.Models;

// depends on the Azure.AI.Inference(pre-release) package
// https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-models/how-to/use-image-embeddings?pivots=programming-language-csharp
public sealed class AzureAiCohereEmbedV3EnglishModel
{
    private readonly string _serviceEndpoint;
    private readonly ILogger<AzureAiCohereEmbedV3EnglishModel> _logger;
    private readonly ImageEmbeddingsClient _imageEmbeddingsClient;
    private readonly EmbeddingsClient _embeddingsClient;

    public AzureAiCohereEmbedV3EnglishModel(
        IConfiguration configuration,
        ILogger<AzureAiCohereEmbedV3EnglishModel> logger)
    {
        _logger = logger;

        string azureInferenceCredential = configuration.GetValue<string>(key: "AzureAiInference:AzureKeyCredential") ?? throw new InvalidOperationException("'AzureInference:Credential' configuration is not set");
        _serviceEndpoint = configuration.GetValue<string>(key: "AzureAiInference:Endpoint") ?? throw new InvalidOperationException("'AzureInference:BaseUrl' configuration is not set");

        _imageEmbeddingsClient = new ImageEmbeddingsClient(new Uri(_serviceEndpoint),
                                                           new AzureKeyCredential(azureInferenceCredential));

        _embeddingsClient = new EmbeddingsClient(new Uri(_serviceEndpoint),
                                                 new AzureKeyCredential(azureInferenceCredential));
    }

    public async Task<float[]> GenerateImageVectorEmbeddingsAsync(string imageFilePath, string imageFormat)
    {
        List<ImageEmbeddingInput> input = [ImageEmbeddingInput.Load(imageFilePath, imageFormat)];

        ImageEmbeddingsOptions requestOptions = new(input)
                                                {
                                                    Model = "Cohere-embed-v3-english"
                                                };

        try
        {
            Response<EmbeddingsResult> response = await _imageEmbeddingsClient.EmbedAsync(requestOptions);

            _logger.LogDebug("Embedding {EmbeddingsResponse}, BaseUrl: {AzureInferenceServiceEndpoint}, Path: {AzureInferenceEmbeddingPath}",
                             JsonSerializer.Serialize(response),
                             _serviceEndpoint,
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

    public async Task<float[]> GenerateTextVectorEmbeddingsAsync(string searchText)
    {
        List<string> input = [searchText];
        EmbeddingsOptions requestOptions = new(input)
                                           {
                                               Model = "Cohere-embed-v3-english"
                                           };

        try
        {
            Response<EmbeddingsResult> response = await _embeddingsClient.EmbedAsync(requestOptions);

            _logger.LogDebug("Embedding {EmbeddingsResponse}, BaseUrl: {AzureInferenceServiceEndpoint}, Path: {AzureInferenceEmbeddingPath}",
                             JsonSerializer.Serialize(response),
                             _serviceEndpoint,
                             "/embeddings");

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