using Microsoft.Extensions.Configuration;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// https://learn.microsoft.com/en-us/dotnet/aspire/database/qdrant-integration?tabs=dotnet-cli
IResourceBuilder<ParameterResource> qdrantApiKeyParameter = builder.AddParameter(name: "QdrantApiKey", secret: true);
string qdrantDbBindMountSource = builder.Configuration.GetValue<string>(key: nameof(qdrantDbBindMountSource)) ?? throw new InvalidOperationException($"'{nameof(qdrantDbBindMountSource)}' configuration is not set");

IResourceBuilder<QdrantServerResource> qdrantDb = builder.AddQdrant(name: "qdrant-db", qdrantApiKeyParameter)
                                                         .WithContainerName("qdrant-db")
                                                         .WithBindMount(qdrantDbBindMountSource, target: "/qdrant/storage")
                                                         .WithLifetime(ContainerLifetime.Persistent);

// https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/ollama?tabs=dotnet-cli%2Cdocker
string ollamaBindMountSource = builder.Configuration.GetValue<string>(key: nameof(ollamaBindMountSource)) ?? throw new InvalidOperationException($"'{nameof(ollamaBindMountSource)}' configuration is not set");
IResourceBuilder<OllamaResource> ollama = builder.AddOllama(name: "ollama")
                                                 .WithContainerName("ollama")
                                                 .WithBindMount(ollamaBindMountSource, target: "/root/.ollama")
                                                 .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<OllamaModelResource> embedding = ollama.AddModel(name: "embedding-model",
                                                                  modelName: "mxbai-embed-large:335m");

IResourceBuilder<ProjectResource> ui = builder.AddProject<Projects.UI>(name: "ui")
                                              .WithReference(qdrantDb)
                                              .WithReference(embedding)
                                              .WaitFor(qdrantDb)
                                              .WaitFor(embedding);

builder.Build().Run();