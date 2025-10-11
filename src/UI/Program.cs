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
builder.Services.AddSingleton<AzureAiCohereEmbedV3EnglishModel>();

builder.Services.AddSingleton<ColorCollection>();
builder.Services.AddSingleton<ImageCollection>();

builder.Services.AddSingleton<QdrantClient>(_ => new QdrantClient(host: "localhost",
                                                                  port: 6334,
                                                                  apiKey: null,
                                                                  https: false));

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