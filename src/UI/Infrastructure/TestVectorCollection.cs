using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace UI.Infrastructure;

public sealed class TestVectorCollection
{
    private const string CollectionName = "test";
    private readonly QdrantClient _client;
    private const ulong Limit = 5; // the 5 closest points

    public TestVectorCollection(QdrantClient client) => _client = client;

    public async Task InitializeAsync()
    {
        bool doesCollectionExist = await _client.CollectionExistsAsync(CollectionName);
        if (!doesCollectionExist)
        {
            VectorParams vectorsConfig = new()
                                         {
                                             Size = 100, Distance = Distance.Cosine
                                         };
            await _client.CreateCollectionAsync(CollectionName, vectorsConfig);
        }
    }

    public async Task AddVectorsAsync()
    {
        Random random = new();
        List<PointStruct> points = Enumerable.Range(1, 100)
                                             .Select(i =>
                                             {
                                                 float[] vectors = Enumerable.Range(1, 100)
                                                                             .Select(_ => (float)random.NextDouble())
                                                                             .ToArray();

                                                 return new PointStruct
                                                        {
                                                            Id = (ulong)i,
                                                            Vectors = vectors,
                                                            Payload =
                                                            {
                                                                ["color"] = "red", ["rand_number"] = i % 10
                                                            }
                                                        };
                                             })
                                             .ToList();

        await _client.UpsertAsync(CollectionName, points);
    }

    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        float[] queryVector) => await _client.SearchAsync(CollectionName,
                                                          queryVector,
                                                          limit: Limit);

    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        float[] queryVector,
        Condition condition) => await _client.SearchAsync(CollectionName,
                                                          queryVector,
                                                          filter: condition,
                                                          limit: Limit);
}