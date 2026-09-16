using AspireAgentObservability.ApiService.Agent;
using AspireAgentObservability.ApiService.Tool;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
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
builder.Services.AddSingleton<KnowledgeBaseTool>();



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

builder.AddAIAgent(KnowledgeBaseAgentFactory.AgentName, KnowledgeBaseAgentFactory.Create);


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

app.MapPost("/api/chat", async(ChatRequest request, [FromKeyedServices("MeetingAssistant")] AIAgent agent) =>
{
    var response = await agent.RunAsync<ChatResponse>(request.Message);
    ChatResponse result = response.Result;
    Console.WriteLine(result.ToString());
    if(result.OutOfContext)
    {
        return Results.Ok(new{ response = "Out of context question" });
    }
    return Results.Ok(new { response = result.Response});
});


app.Run();

record ChatRequest(string Message);

record ChatResponse(
    [property: Description("The assistant's reply to the meeting-related question.")]
    string Response,

    [property: Description("True if the user's question is not related to meetings/scheduling.")]
    bool OutOfContext
    );

