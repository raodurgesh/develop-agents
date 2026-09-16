
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

public record TicketState(
    [property: Description("The unique identifier for the ticket.")]
    string TicketId,
    [property: Description("The current status of the ticket.")]
    string Status,
    [property: Description("The person to whom the ticket is assigned.")]
    string AssignedTo
);



var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");


// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL).AsIChatClient();


