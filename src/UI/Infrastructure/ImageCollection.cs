using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace UI.Infrastructure;

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