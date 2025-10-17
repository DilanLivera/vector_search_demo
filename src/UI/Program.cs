using Aspire.ServiceDefaults;
using Azure;
using Azure.AI.Inference;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using UI;
using UI.Components;
using UI.Components.Pages;
using UI.Data;
using UI.Infrastructure;
using UI.Infrastructure.Collections;
using UI.Infrastructure.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

builder.Services.AddApplicationAuth(builder.Configuration);

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<OllamaMxbaiEmbedLargeModel>();
builder.Services.AddScoped<AzureAiCohereEmbedV3EnglishModel>();

builder.Services.AddScoped<ColorCollection>();
builder.Services.AddScoped<ImageCollection>();

builder.AddQdrantClient(connectionName: "qdrant-db");
builder.AddOllamaApiClient(connectionName: "embedding")
       .AddEmbeddingGenerator();

builder.Services.AddScoped<ImageEmbeddingsClient>(sp =>
{
    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
    string azureInferenceCredential = configuration.GetValue<string>(key: "AzureAiInference:AzureKeyCredential") ?? throw ConfigurationExceptionFactory.CreateException(propertyName: "AzureInference:Credential");
    string serviceEndpoint = configuration.GetValue<string>(key: "AzureAiInference:Endpoint") ?? throw ConfigurationExceptionFactory.CreateException(propertyName: "AzureInference:Endpoint");

    return new ImageEmbeddingsClient(new Uri(serviceEndpoint),
                                     new AzureKeyCredential(azureInferenceCredential));
});

builder.Services.AddScoped<EmbeddingsClient>(sp =>
{
    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
    string azureInferenceCredential = configuration.GetValue<string>(key: "AzureAiInference:AzureKeyCredential") ?? throw ConfigurationExceptionFactory.CreateException(propertyName: "AzureInference:Credential");
    string serviceEndpoint = configuration.GetValue<string>(key: "AzureAiInference:Endpoint") ?? throw ConfigurationExceptionFactory.CreateException(propertyName: "AzureInference:Endpoint");

    return new EmbeddingsClient(new Uri(serviceEndpoint),
                                new AzureKeyCredential(azureInferenceCredential));
});

builder.Services.AddSingleton<CollectionInitializationStatusManager>();

builder.Services.AddSingleton<BlobContainerClient>(sp =>
{
    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
    string connectionString = configuration.GetValue<string>(key: "AzureBlobStorage:ConnectionString") ?? throw ConfigurationExceptionFactory.CreateException(propertyName: "AzureBlobStorage:ConnectionString");
    string blobContainerName = configuration.GetValue<string>(key: "AzureBlobStorage:BlobContainerName") ?? throw ConfigurationExceptionFactory.CreateException(propertyName: "AzureBlobStorage:BlobContainerName");

    return new BlobContainerClient(connectionString, blobContainerName);
});

builder.AddServiceDefaults();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

_ = Task.Run(async () =>
{
    using (IServiceScope scope = app.Services.CreateScope())
    {
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        using (logger.BeginScope(state: "Starting collection initialization..."))
        {

            ColorCollection colorCollection = scope.ServiceProvider.GetRequiredService<ColorCollection>();
            // VoidResult colorCollectionInitializationResult = await colorCollection.InitializeAsync();
            //
            // if (colorCollectionInitializationResult.IsSuccess)
            // {
            //     logger.LogDebug("'{CollectionName}' collection initialization completed successfully", nameof(ColorCollection));
            // }

            ImageCollection imageCollection = scope.ServiceProvider.GetRequiredService<ImageCollection>();
            VoidResult imageCollectionInitializationResult = await imageCollection.InitializeAsync();

            if (imageCollectionInitializationResult.IsSuccess)
            {
                logger.LogDebug("'{CollectionName}' collection initialization completed successfully", nameof(ImageCollection));
            }

            BlobContainerClient blobContainerClient = scope.ServiceProvider.GetRequiredService<BlobContainerClient>();
            Response<bool> doesContainerExist = await blobContainerClient.ExistsAsync();

            if (!doesContainerExist)
            {
                logger.LogInformation("Blob container does not exist");
            }

            // logger.LogInformation("Listing blobs in container:");
            // await foreach (BlobItem blob in  blobContainerClient.GetBlobsAsync())
            // {
            //     logger.LogInformation("- {BlobItemName} {BlobType}", blob.Name, "blob.Properties?.BlobType.Value");
            // }

            string blobName = AzureBlobStorageData.BlobNames[^305];
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            Response<BlobDownloadResult> blobDownloadResult = await blobClient.DownloadContentAsync();

            if (blobDownloadResult.HasValue)
            {
                BinaryData blobContent = blobDownloadResult.Value.Content;
                string imageInBase64String = Convert.ToBase64String(blobContent.ToArray());

                ImageEmbeddingInput input = new($"data:image/jpg;base64,{imageInBase64String}");

                ImageEmbeddingsClient imageEmbeddingsClient = scope.ServiceProvider.GetRequiredService<ImageEmbeddingsClient>();
                ImageEmbeddingsOptions options = new(input: [input])
                                                 {

                                                     InputType = EmbeddingInputType.Document, Model = "Cohere-embed-v3-english"
                                                 };

                Response<EmbeddingsResult> embeddingResult = await imageEmbeddingsClient.EmbedAsync(options);
                float[] embedding = embeddingResult.Value
                                                   .Data
                                                   .Select(i => i.Embedding.ToObjectFromJson<float[]>() ?? throw new InvalidOperationException("Failed to deserialize embedding item."))
                                                   .ToArray()
                                                   .First();

                PointStruct point = new()
                                    {
                                        Id = Guid.NewGuid(),
                                        Vectors = embedding,
                                        Payload =
                                        {
                                            ["image_in_base64_string"] = imageInBase64String, ["image_name"] = blobName, ["format"] = blobContent.MediaType ?? "", ["created_at_utc"] = DateTimeOffset.UtcNow.ToString()
                                        }
                                    };

                QdrantClient qdrantClient = scope.ServiceProvider.GetRequiredService<QdrantClient>();
                UpdateResult updateResult = await qdrantClient.UpsertAsync(collectionName: "images", points: [point]);
            }
        }
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorHandlingPath: "/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseApplicationAuth();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();