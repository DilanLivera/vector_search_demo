using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text.Json;
using UI.Infrastructure.Models;

namespace UI.Infrastructure.Collections.Images;

public sealed class DirectoryImagesCollectionInitializer
{
    private readonly List<string> _images = ["dogs.jpeg", "elephant.jpeg", "parrot.jpeg", "tiger.jpg"];
    private readonly string _imageDirectoryPath;

    private readonly AzureAiCohereEmbedV3EnglishModel _model;
    private readonly ILogger<DirectoryImagesCollectionInitializer> _logger;
    private readonly QdrantClient _qdrantClient;

    public DirectoryImagesCollectionInitializer(
        AzureAiCohereEmbedV3EnglishModel model,
        IConfiguration configuration,
        ILogger<DirectoryImagesCollectionInitializer> logger,
        QdrantClient qdrantClient)
    {
        _logger = logger;
        _model = model;
        _qdrantClient = qdrantClient;

        _imageDirectoryPath = configuration.GetValue<string>(key: "ImagesDirectoryPath") ??
                              throw ConfigurationExceptionFactory.CreateException(propertyName: "ImagesDirectoryPath");
    }

    public async Task<VoidResult> InitializeCollectionAsync(string collectionName)
    {
        var logStateData = new { CollectionName = collectionName };
        KeyValuePair<string, object> logState = new("Adding images to the collection", logStateData);
        using (_logger.BeginScope(logState))
        {
            foreach (string image in _images)
            {
                string imageFilePath = Path.Combine(_imageDirectoryPath, image);

                _logger.LogDebug("Adding '{ImageFilePath}' image to collection", imageFilePath);

                if (!File.Exists(imageFilePath))
                {
                    _logger.LogWarning("'{ImageName}' image does not exist in the '{ImageDirectoryPath}' directory.",
                                       image,
                                       _imageDirectoryPath);

                    continue;
                }

                string imageFormat = Path.GetExtension(imageFilePath);
                try
                {
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

                    UpdateResult upsertResult = await _qdrantClient.UpsertAsync(collectionName, points: [point]);

                    _logger.LogDebug("Upsert result: {UpsertResult}", JsonSerializer.Serialize(upsertResult));
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to initialize the collection");

                    return VoidResult.Failure(exception);
                }
            }
        }

        return VoidResult.Success();
    }
}