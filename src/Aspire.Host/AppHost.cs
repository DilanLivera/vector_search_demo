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

// the following is wip. currently prometheus container fails to start.
IResourceBuilder<ContainerResource> prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v3.2.1")
                                                        .WithBindMount("../../container_volumes/prometheus", "/etc/prometheus")
                                                        .WithArgs("--web.enable-otlp-receiver", "--config.file=/etc/prometheus/prometheus.yaml")
                                                        .WithHttpEndpoint(targetPort: 9090, name: "http");

IResourceBuilder<ContainerResource> grafana = builder.AddContainer("grafana", "grafana/grafana")
                                                     .WithBindMount("../../container_volumes/grafana/config", "/etc/grafana", isReadOnly: true)
                                                     .WithBindMount("../../container_volumes/grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
                                                     .WithEnvironment("PROMETHEUS_ENDPOINT", prometheus.GetEndpoint("http"))
                                                     .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http")
                                                     .WithLifetime(ContainerLifetime.Session); // Force fresh container each run

IResourceBuilder<OpenTelemetryCollectorResource> otelCollector = builder.AddOpenTelemetryCollector("otelcollector")
                                                                        .WithConfig("./config.yaml")
                                                                        .WithEnvironment("PROMETHEUS_ENDPOINT", $"{prometheus.GetEndpoint("http")}/api/v1/otlp")
                                                                        .WithAppForwarding();


builder.Build().Run();