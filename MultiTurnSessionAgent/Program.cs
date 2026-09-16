
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;


var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");


// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL).AsIChatClient();




AIAgent agent = chatClient.AsAIAgent(
    name: "NetworkSupport",
    instructions: "You are a network support agent that helps users with their network issues. You are friendly and helpful. You are also a network expert and can provide detailed technical explanations."
    );

AgentSession session = await agent.CreateSessionAsync();


// Run it.
//string userIssue = "My internet is not working. I have tried restarting my router, but it still doesn't work. Can you help me?";
//Console.WriteLine($"User issue: {userIssue}");
//var response = await agent.RunAsync(userIssue);
//Console.WriteLine(response);


while (true)
{
    Console.WriteLine("Enter your issue (or type 'exit' to quit):");
    var userIssue = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userIssue) || userIssue.ToLower() == "exit")
    {
        break;
    }
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userIssue, session))
    {
        Console.Write(update.Text);
    }
    Console.WriteLine(); // Add a new line after the response
}
