using System.Diagnostics;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using UI.Components.Pages;
using UI.Infrastructure.Models;

namespace UI.Infrastructure.Collections;

public sealed class ImageCollection
{
    private readonly CollectionInitializationStatusManager _statusManager;
    private readonly ILogger<ImageCollection> _logger;
    private readonly AzureAiCohereEmbedV3EnglishModel _model;
    private const string CollectionName = "images";

    private readonly List<string> _images = ["dogs.jpeg", "elephant.jpeg", "parrot.jpeg", "tiger.jpg"];
    private readonly string _imageDirectoryPath;

    private readonly QdrantClient _qdrantClient;
    private const ulong Limit = 5; // the 5 closest points
    private static readonly ActivitySource Source = OtelHelpers.ActivitySource;

    public ImageCollection(
        CollectionInitializationStatusManager statusManager,
        ILogger<ImageCollection> logger,
        AzureAiCohereEmbedV3EnglishModel model,
        QdrantClient qdrantClient,
        IConfiguration configuration)
    {
        _statusManager = statusManager;
        _logger = logger;
        _model = model;
        _qdrantClient = qdrantClient;

        _imageDirectoryPath = configuration.GetValue<string>(key: "ImagesDirectoryPath") ??
                              throw ConfigurationExceptionFactory.CreateException(propertyName: "ImagesDirectoryPath");
    }

    public async Task<VoidResult> InitializeAsync()
    {
        using (Activity? activity = Source.StartActivity(name: OtelHelpers.ActivityNames.ImageCollectionInitializationMessage))
        {
            try
            {
                _statusManager.SetCollectionStatus(nameof(ImageCollection), InitializationStatus.InProgress);

                if (await _qdrantClient.CollectionExistsAsync(CollectionName))
                {
                    await _qdrantClient.DeleteCollectionAsync(CollectionName);
                    activity?.AddEvent(new ActivityEvent(name: "Collection deleted"));

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

                activity?.AddEvent(new ActivityEvent(name: "Collection created"));

                List<PointStruct> points = [];
                foreach (string image in _images)
                {
                    KeyValuePair<string, object> logState = new("Image", image);
                    using (_logger.BeginScope(logState))
                    {
                        string imageFilePath = Path.Combine(_imageDirectoryPath, image);

                        _logger.LogDebug("Loading '{ImageFilePath}' image", imageFilePath);

                        if (!File.Exists(imageFilePath))
                        {
                            _logger.LogWarning("'{ImageName}' image does not exist in the '{ImageDirectoryPath}' directory.",
                                               image,
                                               _imageDirectoryPath);

                            continue;
                        }

                        string imageFormat = Path.GetExtension(imageFilePath);

                        Vectors vectors = await _model.GenerateImageVectorEmbeddingsAsync(imageFilePath, imageFormat);

                        byte[] imageBytes = await File.ReadAllBytesAsync(imageFilePath);
                        string imageInBase64String = Convert.ToBase64String(imageBytes);

                        PointStruct point = new()
                                            {
                                                Id = Guid.NewGuid(),
                                                Vectors = vectors,
                                                Payload =
                                                {
                                                    ["image_in_base64_string"] = imageInBase64String, ["image_name"] = image, ["format"] = imageFormat, ["created_at_utc"] = DateTimeOffset.UtcNow.ToString()
                                                }
                                            };

                        points.Add(point);
                    }
                }

                await _qdrantClient.UpsertAsync(CollectionName, points);
                activity?.AddEvent(new ActivityEvent(name: "Points added to the collection"));

                _statusManager.SetCollectionStatus(nameof(ImageCollection), InitializationStatus.Completed);

                return VoidResult.Success();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to initialize '{CollectionName}'", nameof(ImageCollection));

                _statusManager.SetCollectionStatus(nameof(ImageCollection),
                                                   InitializationStatus.Failed,
                                                   errorMessage: $"Failed to initialize '{CollectionName}' due to '{exception.Message}' error.");

                return VoidResult.Failure(exception);
            }
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