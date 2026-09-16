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



 string CorporatePolicy = @"
 [Effective Jan 2026] ABC Corporate Remote Work Policy :
 - Employees may work remotely up to 3 days per week, subject to manager approval.
 - Employees must be available during core business hours (9 AM - 5 PM) for meetings and collaboration.
 - Hardware  Budget : $500 evry 6 months for remote work equipment (e.g., monitors, ergonomic chairs).
 - Security Requirements : Employees must use company-approved VPN and multi-factor authentication for remote access.
";

Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAdapter(string query, CancellationToken cancellationToken)
{
  
    if (query.Contains("remote work policy", StringComparison.OrdinalIgnoreCase))
    {
        return Task.FromResult<IEnumerable<TextSearchProvider.TextSearchResult>>(new[]
        {
            new TextSearchProvider.TextSearchResult
            {
               SourceName = "ABC Corporate Policy",
               SourceLink = "https://www.abc.com/corporate-policy",
               Text = CorporatePolicy
            }
        });
    }

    if(query.Contains("hardware budget", StringComparison.OrdinalIgnoreCase))
    {
        return Task.FromResult<IEnumerable<TextSearchProvider.TextSearchResult>>(new[]
        {
            new TextSearchProvider.TextSearchResult
            {
               SourceName = "ABC Corporate Policy",
               SourceLink = "https://www.abc.com/corporate-policy",
               Text = @"
                 - Employees may work remotely up to 3 days per week, subject to manager approval.
                 - Employees must be available during core business hours (9 AM - 5 PM) for meetings and collaboration.
                 - Hardware  Budget : $500 evry 6 months for remote work equipment (e.g., monitors, ergonomic chairs).
                 - Security Requirements : Employees must use company-approved VPN and multi-factor authentication for remote access.
                "  
            }
        });
    }

    return Task.FromResult<IEnumerable<TextSearchProvider.TextSearchResult>>(new[] { new TextSearchProvider.TextSearchResult{
      SourceName = "ABC Corporate Policy",
        SourceLink = "https://www.abc.com/corporate-policy",
        Text = CorporatePolicy
    }});
}


AIAgent agent = chatClient.AsAIAgent(
    new ChatClientAgentOptions
    {
        Name = "MeetingAssistant",
        ChatOptions = new ChatOptions()
        {
            Instructions = """
            You are a HR assistant.  
            """,
        },

        AIContextProviders = [new TextSearchProvider(SearchAdapter, new TextSearchProviderOptions { SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke })]
    });




// Run it.
Console.WriteLine($"{agent.Name} Please answer the following questions: ");
var response = await agent.RunAsync("I am a Employee how many days can I work remotely? and what is the hardware budget?");
Console.WriteLine(response);

Console.ReadLine();
