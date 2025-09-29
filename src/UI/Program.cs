using Qdrant.Client;
using UI.Components;
using UI.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

builder.Services.AddApplicationAuth(builder.Configuration);

builder.Services.AddAuthorizationCore();

builder.Services.AddHttpClient<VectorEmbeddingGenerateClient>();

builder.Services.AddSingleton<TestVectorCollection>();
builder.Services.AddSingleton<QdrantClient>(_ => new QdrantClient(host: "localhost"));

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    TestVectorCollection testVectorCollection = scope.ServiceProvider.GetRequiredService<TestVectorCollection>();
    await testVectorCollection.InitializeAsync();
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