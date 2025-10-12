using Qdrant.Client;
using Qdrant.Client.Grpc;
using UI.Infrastructure.Models;

namespace UI.Infrastructure.Collections;

public sealed class ImageCollection
{
    private readonly ILogger<ImageCollection> _logger;
    private readonly AzureAiCohereEmbedV3EnglishModel _model;
    private const string CollectionName = "images";

    private readonly List<string> _images = ["dogs.jpeg", "elephant.jpeg", "parrot.jpeg", "tiger.jpg"];
    private const string ImageDirectoryPath = "/Users/dilan_livera/dev/repositories/vector_search_demo/src/UI/Data";

    private readonly QdrantClient _qdrantClient;
    private const ulong Limit = 5; // the 5 closest points

    public ImageCollection(
        ILogger<ImageCollection> logger,
        AzureAiCohereEmbedV3EnglishModel model,
        QdrantClient qdrantClient)
    {
        _logger = logger;
        _model = model;
        _qdrantClient = qdrantClient;

    }

    public async Task InitializeAsync()
    {
        try
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

                    Vectors vectors = await _model.GenerateImageVectorEmbeddingsAsync(imageFilePath, imageFormat);

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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initialize {CollectionName}", nameof(ColorCollection));
        }
    }

    /// <summary>
    /// Searches for the most similar vectors to the query vector.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scored points.</returns>
    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(string text)
    {
        float[] queryVector = await _model.GenerateTextVectorEmbeddingsAsync(text);

        return await _qdrantClient.SearchAsync(CollectionName,
                                               queryVector,
                                               limit: Limit);
    }

    /// <summary>
    /// Searches for the most similar vectors to the query vector with additional filtering conditions.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <param name="condition">The filtering condition to apply to the search.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scored points.</returns>
    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(string text, Condition condition)
    {
        float[] queryVector = await _model.GenerateTextVectorEmbeddingsAsync(text);

        return await _qdrantClient.SearchAsync(CollectionName,
                                               queryVector,
                                               filter: condition,
                                               limit: Limit);
    }
}