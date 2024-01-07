using Cosmos.Chat.GPT.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
namespace Cosmos.Chat.GPT.Services;

public class CosmosDbService
{
    private readonly Container _container;
    
    public CosmosDbService(string endpoint, string key, string databaseName, string containerName)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNullOrEmpty(databaseName);
        ArgumentNullException.ThrowIfNullOrEmpty(containerName);
        
        CosmosSerializationOptions options = new()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        };
        
        CosmosClient client = new CosmosClientBuilder(endpoint, key)
            .WithSerializerOptions(options)
            .Build();
        
        Database? database = client?.GetDatabase(databaseName);
        Container? container = database?.GetContainer(containerName);
        _container = container ??
                     throw new ArgumentException("Unable to connect to existing Azure Cosmos DB container or database.");
    }

    public async Task<Session> InsertSessionAsync(Session session)
    {
        PartitionKey partitionKey = new(session.SessionId);
        return await _container.CreateItemAsync<Session>(
            item: session,
            partitionKey: partitionKey
        );
    }

    public async Task<Message> InsertMessageAsync(Message message)
    {
        PartitionKey partitionKey = new(message.SessionId);
        Message newMessage = message with { Id = Guid.NewGuid().ToString() };
        return await _container.CreateItemAsync<Message>(
            item: newMessage,
            partitionKey: partitionKey
        );
    }

    public async Task<List<Session>> GetSessionsAsync()
    {
        QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
            .WithParameter("@type", nameof(Session));
        FeedIterator<Session> response = _container.GetItemQueryIterator<Session>(query);
        List<Session> output = new();
        while (response.HasMoreResults)
        {
            FeedResponse<Session> results = await response.ReadNextAsync();
            output.AddRange(results);
        }
        return output;
    }

    public async Task<List<Message>> GetSessionMessagesAsync(string sessionId)
    {
        await Task.Delay(millisecondsDelay: 500);
        return Enumerable.Empty<Message>().ToList();
    }

    public async Task<Session> UpdateSessionAsync(Session session)
    {
        PartitionKey partitionKey = new(session.SessionId);
        return await _container.ReplaceItemAsync(
            item: session,
            id: session.Id,
            partitionKey: partitionKey
        );
    }

    public async Task UpsertSessionBatchAsync(params dynamic[] messages)
    {
        if (messages.Select(m => m.SessionId).Distinct().Count() > 1)
        {
            throw new ArgumentException("All items must have the same partition key.");
        }
        PartitionKey partitionKey = new(messages.First().SessionId);
        //doing transactionally 
        TransactionalBatch batch = _container.CreateTransactionalBatch(partitionKey);
        foreach (dynamic message in messages)
        {
            batch.UpsertItem(message);
        }
        await batch.ExecuteAsync();
    }

    public async Task DeleteSessionAndMessagesAsync(string sessionId)
    {
        PartitionKey partitionKey = new(sessionId);
        QueryDefinition query = new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.id = @sessionId")
            .WithParameter("@sessionId", sessionId);
        FeedIterator<string> response = _container.GetItemQueryIterator<string>(query);
        TransactionalBatch batch = _container.CreateTransactionalBatch(partitionKey);
        while (response.HasMoreResults)
        {
            FeedResponse<string> results = await response.ReadNextAsync();
            foreach (string id in results)
            {
                batch.DeleteItem(id);
            }
        }
        await batch.ExecuteAsync();
    }
}