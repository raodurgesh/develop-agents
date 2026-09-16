using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
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


EmployeeProfileProvider profileProvider = new(chatClient);

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "MeetingAssistant",
    ChatOptions = new ChatOptions
    {
        Instructions = """
        You are a meeting assistant. Extract meeting details from the user's message.
        If the user's question is not related to meetings/scheduling, set OutOfContext to true
        and leave the other fields empty/default
        """
    },
    AIContextProviders = [profileProvider]
});

AgentSession session = await agent.CreateSessionAsync();

// Turn 1: profile unknown -> pre-hook instructs the agent to ask for name/department.
Console.WriteLine(await agent.RunAsync("Book a meeting with John tomorrow at 3pm.", session));

// Turn 2: user reveals profile -> post-hook extracts and stores it in session state.
Console.WriteLine(await agent.RunAsync("I'm Durgesh from the Finance department.", session));

EmployeeProfile profile = profileProvider.GetProfile(session);
Console.WriteLine($"Stored profile -> Name: {profile.EmployeeName}, Department: {profile.Department}");


internal sealed class EmployeeProfileProvider : AIContextProvider
{

    private readonly ProviderSessionState<EmployeeProfile> _sessionState;
    private readonly IChatClient _chatClient;

    public EmployeeProfileProvider(IChatClient chatClient)
    {
        _chatClient = chatClient;
        _sessionState = new ProviderSessionState<EmployeeProfile>(_ => new EmployeeProfile(), this.GetType().Name);
    }

    public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];

    public EmployeeProfile GetProfile(AgentSession session)
    {
        return _sessionState.GetOrInitializeState(session);
    }

    // PRE hook: called before every model invocation. The returned AIContext (instructions/messages/tools)
    // is merged into the request the agent sends to the model.
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        if (context.Session is not AgentSession session)
        {
            return new ValueTask<AIContext>(new AIContext());
        }

        EmployeeProfile profile = GetProfile(session);

        AIContext aiContext = new()
        {
            Instructions = string.IsNullOrEmpty(profile.EmployeeName) || string.IsNullOrEmpty(profile.Department)
                ? "The employee's name and department are not known yet. Before extracting meeting details, ask the user for their name and department."
                : $"The user is {profile.EmployeeName} from the {profile.Department} department. Treat them as the meeting organizer."
        };

        return new ValueTask<AIContext>(aiContext);
    }

    // POST hook: called after each successful invocation with the request/response messages.
    // Extracts the employee profile from the conversation and persists it in the session's state bag.
    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        if (context.Session is not AgentSession session)
        {
            return;
        }

        EmployeeProfile profile = GetProfile(session);
        if (!string.IsNullOrEmpty(profile.EmployeeName) && !string.IsNullOrEmpty(profile.Department))
        {
            return; // already captured
        }

        string conversation = string.Join(
            Environment.NewLine,
            context.RequestMessages
                .Concat(context.ResponseMessages ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Text))
                .Select(m => $"{m.Role}: {m.Text}"));

        if (conversation.Length == 0)
        {
            return;
        }

        var extraction = await _chatClient.GetResponseAsync<EmployeeProfile>(
            [
                new ChatMessage(ChatRole.System, "Extract the employee's name and department from the conversation. Leave a field null when it is not mentioned."),
                new ChatMessage(ChatRole.User, conversation)
            ],
            cancellationToken: cancellationToken);

        if (extraction.TryGetResult(out EmployeeProfile? extracted))
        {
            if (string.IsNullOrEmpty(profile.EmployeeName) && !string.IsNullOrEmpty(extracted.EmployeeName))
            {
                profile.EmployeeName = extracted.EmployeeName;
            }
            if (string.IsNullOrEmpty(profile.Department) && !string.IsNullOrEmpty(extracted.Department))
            {
                profile.Department = extracted.Department;
            }

            _sessionState.SaveState(session, profile);
        }
    }
}


internal sealed class EmployeeProfile
{
    [Description("The employee's name; null if not mentioned.")]
    public string? EmployeeName { get; set; }

    [Description("The employee's department; null if not mentioned.")]
    public string? Department { get; set; }
}
