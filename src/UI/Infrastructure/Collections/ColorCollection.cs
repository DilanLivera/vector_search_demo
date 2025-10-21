using System.Diagnostics;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using UI.Components.Pages;
using UI.Infrastructure.Collections.Images;
using UI.Infrastructure.Models;

namespace UI.Infrastructure.Collections;

// please refer to the https://github.com/qdrant/qdrant-dotnet for details.
/// <summary>
/// Provides functionality for testing vector similarity search using Qdrant.
/// </summary>
public sealed class ColorCollection
{
    private const string CollectionName = "colors";

    private static readonly List<string> Colors =
    [
        "Red",
        "Scarlet",
        "Crimson",
        "Burgundy",
        "Maroon",
        "Ruby",
        "Cerise",
        "Carmine",
        "Barn Red",
        "Coral",
        "Salmon",
        "Pink",
        "Hot Pink",
        "Rose",
        "Fuchsia",
        "Magenta",
        "Raspberry",
        "Lavender Pink",
        "Light Salmon",
        "Dark Red",
        "Orange",
        "Dark Orange",
        "Tangerine",
        "Apricot",
        "Peach",
        "Gold",
        "Yellow",
        "Lemon",
        "Canary",
        "Chartreuse",
        "Mustard",
        "Saffron",
        "Amber",
        "Beige",
        "Khaki",
        "Cream",
        "Papaya Whip",
        "Corn",
        "Citrine",
        "Goldenrod",
        "Green",
        "Lime",
        "Forest Green",
        "Emerald",
        "Jade",
        "Sea Green",
        "Mint",
        "Olive",
        "Sage",
        "Hunter Green",
        "Kelly Green",
        "Spring Green",
        "Dark Green",
        "Aquamarine",
        "Chartreuse Green",
        "Moss Green",
        "Pear",
        "Shamrock Green",
        "Teal Green",
        "Artichoke Green",
        "Blue",
        "Navy",
        "Royal Blue",
        "Sapphire",
        "Azure",
        "Cerulean",
        "Sky Blue",
        "Baby Blue",
        "Turquoise",
        "Cyan",
        "Teal",
        "Indigo",
        "Denim",
        "Periwinkle",
        "Powder Blue",
        "Cadet Blue",
        "Steel Blue",
        "Midnight Blue",
        "Cobalt",
        "Electric Blue",
        "Purple",
        "Violet",
        "Lavender",
        "Plum",
        "Lilac",
        "Amethyst",
        "Mauve",
        "Thistle",
        "Orchid",
        "Byzantium",
        "Black",
        "White",
        "Gray",
        "Silver",
        "Charcoal",
        "Brown",
        "Chocolate",
        "Tan",
        "Sepia",
        "Ivory"
    ];

    private readonly CollectionInitializationStatusManager _statusManager;
    private readonly ILogger<ColorCollection> _logger;
    private readonly QdrantClient _qdrantClient;
    private readonly OllamaMxbaiEmbedLargeModel _model;
    private const ulong Limit = 5; // the 5 closest points
    private static readonly ActivitySource Source = OtelHelpers.ActivitySource;

    public ColorCollection(
        CollectionInitializationStatusManager statusManager,
        ILogger<ColorCollection> logger,
        OllamaMxbaiEmbedLargeModel model,
        QdrantClient qdrantClient)
    {
        _statusManager = statusManager;
        _logger = logger;
        _qdrantClient = qdrantClient;
        _model = model;
    }

    /// <summary>
    /// Ensures the vector collection exists in Qdrant with seed data.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<VoidResult> InitializeAsync()
    {
        using (Activity? activity = Source.StartActivity(name: OtelHelpers.ActivityNames.ColorCollectionInitializationMessage))
        {
            try
            {
                _statusManager.SetCollectionStatus(nameof(ColorCollection), InitializationStatus.InProgress);

                if (await _qdrantClient.CollectionExistsAsync(CollectionName))
                {
                    await _qdrantClient.DeleteCollectionAsync(CollectionName);
                    activity?.AddEvent(new ActivityEvent(name: "Collection deleted"));

                    if (await _qdrantClient.CollectionExistsAsync(CollectionName))
                    {
                        throw new InvalidOperationException($"Failed to delete '{CollectionName}'");
                    }
                }

                VectorParams vectorsConfig = new()
                                             {
                                                 Size = 1024, // this should match the vector dimension of color
                                                 Distance = Distance.Cosine
                                             };
                await _qdrantClient.CreateCollectionAsync(CollectionName, vectorsConfig);

                if (!await _qdrantClient.CollectionExistsAsync(CollectionName))
                {
                    throw new InvalidOperationException($"'{CollectionName}' collection not found");
                }

                activity?.AddEvent(new ActivityEvent(name: "Collection created"));

                List<PointStruct> points = [];
                foreach (string color in Colors)
                {
                    Result<float[]> generateEmbeddingsResult = await _model.GenerateTextVectorEmbeddingsAsync(color);

                    if (generateEmbeddingsResult.IsFailure)
                    {
                        _logger.LogWarning(generateEmbeddingsResult.Error,
                                           "Failed to generate text vector embeddings for '{Color}' color",
                                           color);

                        continue;
                    }

                    PointStruct point = new()
                                        {
                                            Id = (ulong)points.Count + 1,
                                            Vectors = generateEmbeddingsResult.Value,
                                            Payload =
                                            {
                                                ["color"] = color, ["rand_number"] = (points.Count + 1) % 10
                                            }
                                        };
                    points.Add(point);
                }

                await _qdrantClient.UpsertAsync(CollectionName, points);
                activity?.AddEvent(new ActivityEvent(name: "Points added to the collection"));

                _statusManager.SetCollectionStatus(nameof(ColorCollection), InitializationStatus.Completed);

                return VoidResult.Success();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to initialize '{CollectionName}'", nameof(ImageCollection));

                _statusManager.SetCollectionStatus(nameof(ColorCollection),
                                                   InitializationStatus.Failed,
                                                   errorMessage: $"Failed to initialize '{CollectionName}' due to '{exception.Message}' error.");

                return VoidResult.Failure(exception.Message);
            }
        }
    }

    /// <summary>
    /// Searches for the most similar vectors to the query vector.
    /// </summary>
    /// <param name="input">The text input to search for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scored points.</returns>
    public async Task<Result<IReadOnlyList<ScoredPoint>>> SearchAsync(string input)
    {
        Result<float[]> generateEmbeddingsResult = await _model.GenerateTextVectorEmbeddingsAsync(input);

        if (generateEmbeddingsResult.IsFailure)
        {
            return Result<IReadOnlyList<ScoredPoint>>.Failure(generateEmbeddingsResult.Error);
        }

        try
        {
            IReadOnlyList<ScoredPoint> searchResult = await _qdrantClient.SearchAsync(CollectionName,
                                                                                      generateEmbeddingsResult.Value,
                                                                                      limit: Limit);

            return Result<IReadOnlyList<ScoredPoint>>.Success(searchResult);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "'{Input}' search Failed", input);

            return Result<IReadOnlyList<ScoredPoint>>.Failure(exception);
        }
    }

    /// <summary>
    /// Searches for the most similar vectors to the query vector with additional filtering conditions.
    /// </summary>
    /// <param name="input">The color to search for.</param>
    /// <param name="condition">The filtering condition to apply to the search.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scored points.</returns>
    public async Task<Result<IReadOnlyList<ScoredPoint>>> SearchAsync(string input, Condition condition)
    {
        Result<float[]> generateEmbeddingsResult = await _model.GenerateTextVectorEmbeddingsAsync(input);

        try
        {
            IReadOnlyList<ScoredPoint> searchResult = await _qdrantClient.SearchAsync(CollectionName,
                                                                                      generateEmbeddingsResult.Value,
                                                                                      filter: condition,
                                                                                      limit: Limit);

            return Result<IReadOnlyList<ScoredPoint>>.Success(searchResult);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "'{Input}' search Failed", input);

            return Result<IReadOnlyList<ScoredPoint>>.Failure(exception);
        }
    }
}