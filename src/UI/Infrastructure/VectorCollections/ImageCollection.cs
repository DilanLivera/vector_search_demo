using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace UI.Infrastructure.VectorCollections;

public sealed class ImageCollection
{
    private readonly ILogger<ImageCollection> _logger;
    private readonly ImageVectorEmbeddingGenerateClient _embeddingGenerateClient;
    private const string CollectionName = "images";

    private readonly List<string> _images = ["dogs.jpeg", "elephant.jpeg", "parrot.jpeg", "tiger.jpg"];
    private const string ImageDirectoryPath = "/Users/dilan_livera/dev/repositories/vector_search_demo/src/UI/Data";

    private readonly QdrantClient _qdrantClient;
    private const ulong Limit = 5; // the 5 closest points

    public ImageCollection(
        ILogger<ImageCollection> logger,
        ImageVectorEmbeddingGenerateClient embeddingGenerateClient,
        QdrantClient qdrantClient)
    {
        _logger = logger;
        _embeddingGenerateClient = embeddingGenerateClient;
        _qdrantClient = qdrantClient;

    }

    public async Task InitializeAsync()
    {
        if (await _qdrantClient.CollectionExistsAsync(CollectionName))
        {
            await _qdrantClient.DeleteCollectionAsync(CollectionName);

            if (await _qdrantClient.CollectionExistsAsync(CollectionName))
            {
                throw new InvalidOperationException($"Failed to delete '{CollectionName}'");
            }
        }

        VectorParams vectorsParams = new()
                                     {
                                         Size = 1024, // this should match the vector dimension of image
                                         Distance = Distance.Cosine
                                     };
        await _qdrantClient.CreateCollectionAsync(CollectionName, vectorsParams);

        if (!await _qdrantClient.CollectionExistsAsync(CollectionName))
        {
            throw new InvalidOperationException($"'{CollectionName}' collection not found");
        }

        List<PointStruct> points = [];
        foreach (string image in _images)
        {
            KeyValuePair<string, object> logState = new("Image", image);
            using (_logger.BeginScope(logState))
            {
                string imageFilePath = Path.Combine(ImageDirectoryPath, image);

                _logger.LogDebug("Loading '{ImageFilePath}' image", imageFilePath);

                if (!File.Exists(imageFilePath))
                {
                    _logger.LogWarning("'{ImageName}' image does not exist in the '{ImageDirectoryPath}' directory.",
                                       image,
                                       ImageDirectoryPath);

                    continue;
                }

                string imageFormat = Path.GetExtension(imageFilePath);

                Vectors vectors = await _embeddingGenerateClient.GenerateVectorEmbeddings(imageFilePath, imageFormat);

                PointStruct point = new()
                                    {
                                        Id = Guid.NewGuid(),
                                        Vectors = vectors,
                                        Payload =
                                        {
                                            ["image"] = image, ["format"] = imageFormat, ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString()
                                        }
                                    };

                points.Add(point);
            }
        }

        await _qdrantClient.UpsertAsync(CollectionName, points);
    }
}

// depends on the Azure.AI.Inference(pre-release) and Azure.Identity packages
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