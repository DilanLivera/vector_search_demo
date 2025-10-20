using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
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
                string imageInBase64String = Convert.ToBase64String(blobContent.ToArray());

                float[] embedding = await _model.GenerateImageVectorEmbeddingsFromBase64StringAsync(imageInBase64String, imageFormat: "image/jpg");

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