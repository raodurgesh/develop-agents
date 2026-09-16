using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;


var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");



var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<Tool>();



// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true)
    .Build();

builder.Services.AddSingleton(chatClient);

builder.AddAIAgent("TriageAgent", (sp, name) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return new ChatClientAgent(
        chatClient: chatClient,
        instructions:
        """
        You are a routing manager.
        Analysis the incoming request and determine which agent is best suited to handle it.
        Do not attempt to answer the request yourself. You are only responsible for routing the request to the appropriate agent.
        Keep your response to 1-2 sentences.
        """,
        name: name,
        description: "Routes requests to the correct specialized agent.",
        tools:null,
        loggerFactory: sp.GetRequiredService<ILoggerFactory>(),
        services: sp
        );
});

builder.AddAIAgent("OrderAgent", (sp, name) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var tool = sp.GetRequiredService<Tool>();
    return new ChatClientAgent(
        chatClient: chatClient,
        instructions:
        """
        You are a specialized agent that handles order requests.
        You handle replacements, tracking and shipping preferences.
        """,
        name: name,
        description: "Handles order requests.",
        tools: null,
        loggerFactory: sp.GetRequiredService<ILoggerFactory>(),
        services: sp
        );
});

builder.AddAIAgent("RefundAgent", (sp, name) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var tool = sp.GetRequiredService<Tool>();
    return new ChatClientAgent(
        chatClient: chatClient,
        instructions:
        """
        You are a finance specialized agent that handles refund requests.
        You looks up order details, gather context, and process refund requests accordingly.
        """,
        name: name,
        description: "Handles refund requests.",
        tools: null,
        loggerFactory: sp.GetRequiredService<ILoggerFactory>(),
        services: sp
        );
});

// SEQUENTIAL WORKFLOW :


builder.AddWorkflow("OrderRefundWorkFlow_Squential", (sp, name) =>
{
    var triageAgent = sp.GetRequiredKeyedService<AIAgent>("TriageAgent"); 
    var orderAgent = sp.GetRequiredKeyedService<AIAgent>("OrderAgent");    
    var refundAgent = sp.GetRequiredKeyedService<AIAgent>("RefundAgent"); 

    var workflow = AgentWorkflowBuilder.BuildSequential(name, triageAgent, orderAgent, refundAgent);

    return workflow;
}).AddAsAIAgent();

// GROUP CHAT WORKFLOW :

builder.AddWorkflow("OrderRefundWorkFlow_GroupChat", (sp, name) =>
{
    var triageAgent = sp.GetRequiredKeyedService<AIAgent>("TriageAgent");
    var orderAgent = sp.GetRequiredKeyedService<AIAgent>("OrderAgent");
    var refundAgent = sp.GetRequiredKeyedService<AIAgent>("RefundAgent");
    var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents =>
    new RoundRobinGroupChatManager(agents)
    {
        MaximumIterationCount = 3 // One turn per agent.
    })
    .AddParticipants(triageAgent, orderAgent, refundAgent)
    .WithName(name)
    .WithDescription("A workflow that routes requests to the correct specialized agent and allows them to collaborate in a group chat.")
    .Build();

    return workflow;
}).AddAsAIAgent();



builder.AddDevUI();
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();



var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

// Add DevUI to the pipeline.
app.UseExceptionHandler();
app.MapDevUI();
app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapPost("/api/chat", async (ChatRequest request, [FromKeyedServices("MeetingAssistant")] AIAgent agent) =>
{
    var response = await agent.RunAsync<ChatResponse>(request.Message);
    ChatResponse result = response.Result;
    Console.WriteLine(result.ToString());
    if (result.OutOfContext)
    {
        return Results.Ok(new { response = "Out of context question" });
    }
    return Results.Ok(new { response = result.Response });
});


app.Run();

record ChatRequest(string Message);

record ChatResponse(
    [property: Description("The assistant's reply to the meeting-related question.")]
    string Response,

    [property: Description("True if the user's question is not related to meetings/scheduling.")]
    bool OutOfContext
    );

