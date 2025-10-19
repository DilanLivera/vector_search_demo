using System.Diagnostics;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using UI.Components.Pages;
using UI.Infrastructure.Models;

namespace UI.Infrastructure.Collections.Images;

public sealed class ImageCollection
{
    private const string CollectionName = "images";
    private const ulong Limit = 5; // the 5 closest points
    private static readonly ActivitySource Source = OtelHelpers.ActivitySource;

    private readonly AzureAiCohereEmbedV3EnglishModel _model;
    private readonly CollectionInitializationStatusManager _statusManager;
    private readonly DirectoryImagesCollectionInitializer _collectionInitializer;
    private readonly ILogger<ImageCollection> _logger;
    private readonly QdrantClient _qdrantClient;

    public ImageCollection(
        AzureAiCohereEmbedV3EnglishModel model,
        CollectionInitializationStatusManager statusManager,
        DirectoryImagesCollectionInitializer collectionInitializer,
        ILogger<ImageCollection> logger,
        QdrantClient qdrantClient)
    {
        _statusManager = statusManager;
        _collectionInitializer = collectionInitializer;
        _logger = logger;
        _model = model;
        _qdrantClient = qdrantClient;
    }

    public async Task<VoidResult> InitializeAsync()
    {
        using (Activity? activity = Source.StartActivity(name: OtelHelpers.ActivityNames.ImageCollectionInitializationMessage))
        {
            _statusManager.SetCollectionStatus(nameof(ImageCollection), InitializationStatus.InProgress);

            try
            {

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
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to initialize '{CollectionName}'", nameof(ImageCollection));

                _statusManager.SetCollectionStatus(nameof(ImageCollection),
                                                   InitializationStatus.Failed,
                                                   errorMessage: $"Failed to initialize '{CollectionName}' due to '{exception.Message}' error.");

                return VoidResult.Failure(exception);
            }

            VoidResult result = await _collectionInitializer.InitializeCollectionAsync(CollectionName);

            if (!result.IsSuccess)
            {
                _statusManager.SetCollectionStatus(nameof(ImageCollection),
                                                   InitializationStatus.Failed,
                                                   errorMessage: $"Failed to initialize '{CollectionName}' due to '{result.Error.Message}' error.");

                return VoidResult.Failure(result.Error);
            }

            activity?.AddEvent(new ActivityEvent(name: "Points added to the collection"));

            _statusManager.SetCollectionStatus(nameof(ImageCollection), InitializationStatus.Completed);

            return VoidResult.Success();
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