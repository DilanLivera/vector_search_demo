using Azure;
using Azure.AI.Inference;
using Qdrant.Client;
using UI.Components;
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

builder.Services.AddHttpClient<OllamaMxbaiEmbedLargeModel>();
builder.Services.AddScoped<AzureAiCohereEmbedV3EnglishModel>();

builder.Services.AddScoped<ColorCollection>();
builder.Services.AddScoped<ImageCollection>();

builder.Services.AddScoped<QdrantClient>(_ => new QdrantClient(host: "localhost",
                                                               port: 6334,
                                                               apiKey: null,
                                                               https: false));

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

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    ColorCollection colorCollection = scope.ServiceProvider.GetRequiredService<ColorCollection>();
    await colorCollection.InitializeAsync();

    ImageCollection imageCollection = scope.ServiceProvider.GetRequiredService<ImageCollection>();
    await imageCollection.InitializeAsync();
}

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