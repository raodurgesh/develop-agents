
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;


var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");


// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL).AsIChatClient();




AIAgent agent = chatClient.AsAIAgent(
    name: "MeetingAssistant",
    instructions: """
    You are a meeting assistant. Extract meeting details from the user's message.
    If the user's question is not related to meetings/scheduling, set OutOfContext to true
    and leave the other fields empty/default 
    """
    );


// Run it.
var response = await agent.RunAsync<MeetingDetails>("Say hello in one sentence.");
Console.WriteLine(response);

Console.ReadLine();


// Data Contract
public record MeetingDetails(
    [property: Description("The meeting title. Empty if the question is out of context.")]
    string Title,

    [property: Description("Meeting start time. Use DateTime.MinValue if out of context.")]
    DateTime StartTime,

    [property: Description("Meeting end time. Use DateTime.MinValue if out of context.")]
    DateTime EndTime,

    [property: Description("Participant names. Empty array if out of context.")]
    string[] Participants,

    [property: Description("The meeting agenda. Empty if out of context.")]
    string Agenda,

    [property: Description("Set to true if the user's question is NOT about meetings/scheduling and is therefore out of context; otherwise false.")]
    bool OutOfContext);