using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace UI.Infrastructure;

// please refer to the https://github.com/qdrant/qdrant-dotnet for details.
/// <summary>
/// Provides functionality for testing vector similarity search using Qdrant.
/// </summary>
public sealed class TestVectorCollection
{
    private const string CollectionName = "test";
    private readonly QdrantClient _client;
    private const ulong Limit = 5; // the 5 closest points

    public TestVectorCollection(QdrantClient client) => _client = client;

    /// <summary>
    /// Ensures the vector collection exists in Qdrant.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Adds 100 random vectors to the collection.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Searches for the most similar vectors to the query vector.
    /// </summary>
    /// <param name="queryVector">The vector to search for similar vectors.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scored points.</returns>
    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        float[] queryVector) => await _client.SearchAsync(CollectionName,
                                                          queryVector,
                                                          limit: Limit);

    /// <summary>
    /// Searches for the most similar vectors to the query vector with additional filtering conditions.
    /// </summary>
    /// <param name="queryVector">The vector to search for similar vectors.</param>
    /// <param name="condition">The filtering condition to apply to the search.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scored points.</returns>
    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        float[] queryVector,
        Condition condition) => await _client.SearchAsync(CollectionName,
                                                          queryVector,
                                                          filter: condition,
                                                          limit: Limit);
}