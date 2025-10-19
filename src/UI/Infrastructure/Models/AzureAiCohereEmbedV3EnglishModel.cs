using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;

namespace UI.Infrastructure.Models;

// depends on the Azure.AI.Inference(pre-release) package
// https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-models/how-to/use-image-embeddings?pivots=programming-language-csharp
public sealed class AzureAiCohereEmbedV3EnglishModel
{
    private readonly ILogger<AzureAiCohereEmbedV3EnglishModel> _logger;
    private readonly ImageEmbeddingsClient _imageEmbeddingsClient;
    private readonly EmbeddingsClient _embeddingsClient;

    public AzureAiCohereEmbedV3EnglishModel(
        EmbeddingsClient embeddingsClient,
        ILogger<AzureAiCohereEmbedV3EnglishModel> logger,
        ImageEmbeddingsClient imageEmbeddingsClient)
    {
        _logger = logger;
        _imageEmbeddingsClient = imageEmbeddingsClient;
        _embeddingsClient = embeddingsClient;
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

            _logger.LogDebug("Embedding Response: {EmbeddingsResponse}", JsonSerializer.Serialize(response));

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

    public async Task<float[]> GenerateImageVectorEmbeddingsFromBase64StringAsync(string imageInBase64String, string imageFormat)
    {
        ImageEmbeddingInput input = new($"data:{imageFormat};base64,{imageInBase64String}");

        ImageEmbeddingsOptions options = new(input: [input])
                                         {

                                             InputType = EmbeddingInputType.Document, Model = "Cohere-embed-v3-english"
                                         };

        try
        {
            Response<EmbeddingsResult> response = await _imageEmbeddingsClient.EmbedAsync(options);

            _logger.LogDebug("Embedding Response: {EmbeddingsResponse}", JsonSerializer.Serialize(response));

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

            _logger.LogDebug("Embedding Response: {EmbeddingsResponse}", JsonSerializer.Serialize(response));

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