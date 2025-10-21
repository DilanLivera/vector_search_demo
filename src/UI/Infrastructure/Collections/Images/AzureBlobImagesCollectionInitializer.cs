using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SkiaSharp;
using System.Text.Json;
using UI.Data;
using UI.Infrastructure.Models;

namespace UI.Infrastructure.Collections.Images;

public sealed class AzureBlobImagesCollectionInitializer
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly AzureAiCohereEmbedV3EnglishModel _model;
    private readonly ILogger<AzureBlobImagesCollectionInitializer> _logger;
    private readonly QdrantClient _qdrantClient;

    public AzureBlobImagesCollectionInitializer(
        AzureAiCohereEmbedV3EnglishModel model,
        BlobContainerClient blobContainerClient,
        ILogger<AzureBlobImagesCollectionInitializer> logger,
        QdrantClient qdrantClient)
    {
        _model = model;
        _blobContainerClient = blobContainerClient;
        _logger = logger;
        _qdrantClient = qdrantClient;
    }

    public async Task<VoidResult> InitializeCollectionAsync(string collectionName)
    {
        try
        {
            Response<bool> doesContainerExist = await _blobContainerClient.ExistsAsync();

            if (!doesContainerExist)
            {
                _logger.LogInformation("Blob container does not exist");

                return VoidResult.Failure(errorMessage: "Blob container does not exist");
            }

            // _logger.LogInformation("Listing blobs in container:");
            // await foreach (BlobItem blob in _blobContainerClient.GetBlobsAsync())
            // {
            //     _logger.LogInformation("- {BlobItemName} {BlobType}", blob.Name, "blob.Properties?.BlobType.Value");
            // }

            foreach (string blobName in AzureBlobStorageData.BlobNames)
            {
                BlobClient blobClient = _blobContainerClient.GetBlobClient(blobName);
                Response<BlobDownloadResult> blobDownloadResult = await blobClient.DownloadContentAsync();

                if (!blobDownloadResult.HasValue)
                {
                    continue;
                }

                BinaryData blobContent = blobDownloadResult.Value.Content;

                // reduce quality
                const int quality = 25;
                using SKCodec? codec = SKCodec.Create(blobContent.ToStream());
                using SKBitmap? bitmap = SKBitmap.Decode(codec);
                using SKData? result = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
                _logger.LogInformation("Image quality is reduced to '{Quality}%'. Before size: {SizeBefore}, After size: {SizeAfter}",
                                       quality, bitmap.ByteCount, result.Size);

                string encodedImageInBase64String = Convert.ToBase64String(result.ToArray());

                float[] embedding = await _model.GenerateImageVectorEmbeddingsFromBase64StringAsync(encodedImageInBase64String,
                                                                                                    imageFormat: "image/jpeg");

                string imageInBase64String = Convert.ToBase64String(blobContent.ToArray());
                PointStruct point = new()
                                    {
                                        Id = Guid.NewGuid(),
                                        Vectors = embedding,
                                        Payload =
                                        {
                                            ["image_in_base64_string"] = imageInBase64String, ["image_name"] = blobName, ["format"] = blobContent.MediaType ?? "", ["created_at_utc"] = DateTimeOffset.UtcNow.ToString()
                                        }
                                    };

                UpdateResult upsertResult = await _qdrantClient.UpsertAsync(collectionName, points: [point]);

                _logger.LogDebug("Upserted {CollectionName}. Upsert result: {UpsertResult}",
                                 collectionName,
                                 JsonSerializer.Serialize(upsertResult));
            }

            return VoidResult.Success();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initialize the '{CollectionName}' collection", nameof(ImageCollection));

            return VoidResult.Failure(exception);
        }
    }
}