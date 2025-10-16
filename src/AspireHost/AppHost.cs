IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// https://learn.microsoft.com/en-us/dotnet/aspire/database/qdrant-integration?tabs=dotnet-cli
IResourceBuilder<ParameterResource> qdrantApiKeyParameter = builder.AddParameter(name: "QdrantApiKey", secret: true);
IResourceBuilder<QdrantServerResource> vectorDb = builder.AddQdrant(name: "qdrant-db", qdrantApiKeyParameter)
                                                         .WithContainerName("vector-search-demo-qdrant-db")
                                                         .WithBindMount(source: "/Users/dilan_livera/dev/repositories/vector_search_demo/container_volumes/qdrant",
                                                                        target: "/qdrant/storage")
                                                         .WithLifetime(ContainerLifetime.Persistent);

// https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/ollama?tabs=dotnet-cli%2Cdocker
IResourceBuilder<OllamaResource> ollama = builder.AddOllama(name: "ollama")
                                                 .WithContainerName("vector-search-demo-ollama")
                                                 .WithExternalHttpEndpoints()
                                                 .WithBindMount(source: "/Users/dilan_livera/dev/repositories/vector_search_demo/container_volumes/ollama",
                                                                target: "/root/.ollama")
                                                 .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<OllamaModelResource> embedding = ollama.AddModel(name: "embedding",
                                                                  modelName: "mxbai-embed-large:335m");

IResourceBuilder<ProjectResource> ui = builder.AddProject<Projects.UI>(name: "ui")
                                              .WithReference(vectorDb)
                                              .WithReference(embedding)
                                              .WaitFor(vectorDb)
                                              .WaitFor(embedding);

builder.Build().Run();