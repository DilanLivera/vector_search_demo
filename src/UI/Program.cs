using Azure;
using Azure.AI.Inference;
using UI.Components;
using UI.Components.Pages;
using UI.Infrastructure;
using UI.Infrastructure.Collections;
using UI.Infrastructure.Models;
using Vector.Search.Demo.ServiceDefaults;

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
    string azureInferenceCredential = configuration.GetValue<string>(key: "AzureAiInference:AzureKeyCredential") ?? throw new InvalidOperationException("'AzureInference:Credential' configuration is not set");
    string serviceEndpoint = configuration.GetValue<string>(key: "AzureAiInference:Endpoint") ?? throw new InvalidOperationException("'AzureInference:Endpoint' configuration is not set");

    return new ImageEmbeddingsClient(new Uri(serviceEndpoint),
                                     new AzureKeyCredential(azureInferenceCredential));
});

builder.Services.AddScoped<EmbeddingsClient>(sp =>
{
    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
    string azureInferenceCredential = configuration.GetValue<string>(key: "AzureAiInference:AzureKeyCredential") ?? throw new InvalidOperationException("'AzureInference:Credential' configuration is not set");
    string serviceEndpoint = configuration.GetValue<string>(key: "AzureAiInference:Endpoint") ?? throw new InvalidOperationException("'AzureInference:Endpoint' configuration is not set");

    return new EmbeddingsClient(new Uri(serviceEndpoint),
                                new AzureKeyCredential(azureInferenceCredential));
});

builder.Services.AddSingleton<CollectionInitializationStatusManager>();

builder.AddServiceDefaults();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

_ = Task.Run(async () =>
{
    using (IServiceScope scope = app.Services.CreateScope())
    {
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        using (logger.BeginScope("Starting collection initialization..."))
        {
            try
            {
                ColorCollection colorCollection = scope.ServiceProvider.GetRequiredService<ColorCollection>();
                await colorCollection.InitializeAsync();

                logger.LogDebug("'{CollectionName}' collection initialization completed successfully", nameof(ColorCollection));

                ImageCollection imageCollection = scope.ServiceProvider.GetRequiredService<ImageCollection>();
                await imageCollection.InitializeAsync();

                logger.LogDebug("'{CollectionName}' collection initialization completed successfully", nameof(ImageCollection));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Collection initialization failed");
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