
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;


var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");

AIFunction rawMeetingScheduler = AIFunctionFactory.Create(MeetingTools.ScheduleMeeting);
AIFunction secureMeetingScheduler = new ApprovalRequiredAIFunction(rawMeetingScheduler);


// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL).AsIChatClient();




AIAgent agent = chatClient.AsAIAgent(
    name: "MeetingAssistant",
    instructions: """
    You are a meeting scheduling assistant. You can schedule meetings, check availability, and send invites. 
    When scheduling a meeting, you must use the 'ScheduleMeeting' function.
    """,
    tools: [
        secureMeetingScheduler
    ]
    );

AgentSession session =  await agent.CreateSessionAsync();
Console.WriteLine("Agent session created. You can now interact with the agent.");


// Run it.
var response = await agent.RunAsync<MeetingDetails>("Schedule a meeting with the team to discuss project updates next Monday at 10 AM for 1 hour. Include Alice, Bob, and Charlie as participants. The agenda is to review the current progress and plan next steps.", session);
Console.WriteLine(response);

var approvalRequest = response.Messages.SelectMany(x=> x.Contents).OfType<ToolApprovalRequestContent>().ToList();

if(approvalRequest.Any())
{
    ToolApprovalRequestContent request = approvalRequest.First();
    var requestToolCall = (FunctionCallContent)request.ToolCall;
    string toolName = requestToolCall.Name;
    string toolArgs = JsonSerializer.Serialize(requestToolCall.Arguments);

    // display the approval request to the user and ask for approval
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Approval request for tool '{toolName}' with arguments: {toolArgs}");
    Console.WriteLine("Do you approve this request? (Y/N)");
    Console.ResetColor();

    bool isApproved = Console.ReadLine()?.Trim().ToLower() == "y";

    var approvalMessage = new ChatMessage(
        role: ChatRole.User,
        new[] {request.CreateResponse(isApproved) }
    );

    response = await agent.RunAsync<MeetingDetails>(approvalMessage, session);
    Console.WriteLine(response.Text);

}

Console.WriteLine("DONE!");
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

public static class MeetingTools
{
    [Description("Schedules a meeting with the given details . Use this ONLY when the user explicitly requests to schedule a meeting.")]
    public static string ScheduleMeeting([Description("The meeting title.")] string title, [Description("Meeting start time.")] DateTime startTime, [Description("Meeting end time.")] DateTime endTime, [Description("Participant names.")] string[] participants, [Description("The meeting agenda.")] string agenda)
    {
        // Simulate scheduling a meeting and returning a confirmation message.
        return $"Meeting '{title}' scheduled from {startTime} to {endTime} with participants: {string.Join(", ", participants)}. Agenda: {agenda}";
    }
}
