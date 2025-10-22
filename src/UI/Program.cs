using Aspire.ServiceDefaults;
using Azure;
using Azure.AI.Inference;
using Azure.Storage.Blobs;
using UI;
using UI.Components;
using UI.Components.Pages;
using UI.Infrastructure;
using UI.Infrastructure.Collections;
using UI.Infrastructure.Collections.Images;
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
builder.AddOllamaApiClient(connectionName: "embedding-model")
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

builder.Services.AddScoped<DirectoryImagesCollectionInitializer>();
builder.Services.AddScoped<AzureBlobImagesCollectionInitializer>();

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