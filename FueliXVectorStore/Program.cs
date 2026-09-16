using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using QdrantVectorStore;
using System.ClientModel;
using System.ComponentModel;
using QdrantVectorStoreType = Microsoft.SemanticKernel.Connectors.Qdrant.QdrantVectorStore;



var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");


// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL).AsIChatClient();


// 2. intialize the Embedding Generator with the Fuelix endpoint and API key.

var embeddingGenerator = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();

// 3. Connect to the Qdrant VectorDatabase

var qdrantClient = new QdrantClient("localhost", 6334);
var vectorStore = new QdrantVectorStoreType(qdrantClient, ownsClient: true);

// Get the specific  collection conatining our ArchitectureDecision documents.
var adrCollection = vectorStore.GetCollection<Guid, ArchitectureDecision>("enterprise-adrs");
await adrCollection.EnsureCollectionExistsAsync();

// seed the collection with some sample ArchitectureDecision documents if the collection is empty.

var sampleAdrs = new List<ArchitectureDecision>
{
    new ArchitectureDecision
    {
        Title = "Use Microservices Architecture",
        Content = "We will use a microservices architecture to allow for independent deployment and scaling of services."
    },
    new ArchitectureDecision
    {
        Title = "Use Event-Driven Architecture",
        Content = "We will use an event-driven architecture to allow for asynchronous communication between services."
    },
    new ArchitectureDecision
    {
        Title = "Use CQRS Pattern",
        Content = "We will use the Command Query Responsibility Segregation (CQRS) pattern to separate read and write operations."
    },
    new ArchitectureDecision
    {
        Title = "Use Domain-Driven Design",
        Content = "We will use Domain-Driven Design (DDD) to model our domain and create a ubiquitous language."
    }
};

sampleAdrs.ForEach(async adr =>
{
    // Generate the embedding for the content of the ArchitectureDecision document.
    var embedding = await embeddingGenerator.GenerateAsync(adr.Content);
    adr.ContentVector = embedding.Vector;
    // Add the ArchitectureDecision document to the Qdrant collection.
    await adrCollection.UpsertAsync(adr);
});

Console.WriteLine($"Seeded the Qdrant collection with sample {sampleAdrs.Count} documents.\n");

// Configure the TextSerchProvider options for RAG behavior.

TextSearchProviderOptions textSearchProviderOptions = new TextSearchProviderOptions
{
    SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke
};

// Create a Vector Search Adapter

async Task<IEnumerable<TextSearchProvider.TextSearchResult>>VectorSearchAdapter(string query, CancellationToken cancellationToken)
{
    // Generate the embedding for the query.
    var queryEmbedding = await embeddingGenerator.GenerateAsync(query, cancellationToken : cancellationToken);
    var queryVector = queryEmbedding.Vector;

    // Search the Qdrant collection for the most similar documents to the query.
    var searchOptions = new VectorSearchOptions<ArchitectureDecision>();
    var searchResults = adrCollection.SearchAsync(queryVector, 3, searchOptions, cancellationToken);
    // Map the search results to TextSearchProvider.TextSearchResult objects.
    var result = new List<TextSearchProvider.TextSearchResult>();
    await foreach (var searchResult in searchResults)
        result.Add(new TextSearchProvider.TextSearchResult
            {
               SourceName = $"ADR :{searchResult.Record.Title}",
               SourceLink = $"adr://{searchResult.Record.DocumentId}",
               Text = $"Title : {searchResult.Record.Title}\n Content : {searchResult.Record.Content}"
             }
        );
    return result;
}

// Initialize the Agent with the chatClient, VectorSearchAdapter and TextSearchProviderOptions.

AIAgent architectAgent = chatClient.AsAIAgent(new ChatClientAgentOptions()
{
    Name = "ArchitectAgent",
    ChatOptions = new()
    {
        Instructions = "You are a senior software architect. You will answer questions about software architecture decisions based on the information provided in the retrieved documents.",
    },
    AIContextProviders = new[] { new TextSearchProvider(VectorSearchAdapter, textSearchProviderOptions) }
});

Console.WriteLine("--- Ask the Architect Agent questions about software architecture decisions. Type 'exit' to quit. ---\n");

var query = Console.ReadLine();

Console.WriteLine($"User : {query}");

AgentResponse response = await architectAgent.RunAsync(query);

Console.WriteLine($"Agent : {response.Text}\n");   

Console.ReadLine();