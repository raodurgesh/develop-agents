
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;





var MODEL = Environment.GetEnvironmentVariable("MODEL") ?? "gpt-5-mini";
string FUELIX_API_KEY = Environment.GetEnvironmentVariable("FUELIX_API_KEY") ?? throw new InvalidOperationException("FUELIX_API_KEY env var is not set.");
string FUELIX_ENDPOINT = Environment.GetEnvironmentVariable("FUELIX_ENDPOINT") ?? throw new InvalidOperationException("FUELIX_ENDPOINT env var is not set.");


// Create an chatClient with the Fuelix.
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential(FUELIX_API_KEY),
    new OpenAIClientOptions { Endpoint = new Uri(FUELIX_ENDPOINT) }
    ).GetChatClient(MODEL).AsIChatClient();



AIAgent hardwareAgent = chatClient.AsAIAgent(
    name: "HardwareSupport",
    instructions: "You are a hardware support agent that helps users with their hardware issues. You are friendly and helpful. You are also a hardware expert and can provide detailed technical explanations."
    );

AIAgent softwareAgent = chatClient.AsAIAgent(
    name: "SoftwareSupport",
    instructions: "You are a software support agent that helps users with their software issues. You are friendly and helpful. You are also a software expert and can provide detailed technical explanations."
    );

AIAgent triageAgent = chatClient.AsAIAgent(
    name: "TriageAgent",
    instructions: "You are a triage agent that helps users with their issues. You are friendly and helpful. You are also an expert in triaging issues and can Categories strictly as Hardware, Software, or Other."
    );


Func<TicketDetails, TicketDetails> triageWorkflow =  (TicketDetails ticketDetails) =>
{
    Console.WriteLine($"[Triage] Processing ticket: {ticketDetails.Description}");
    // Use the triage agent to categorize the issue
    var triageResponse =  triageAgent.RunAsync<TicketDetails>($"Categorize the following issue: {ticketDetails.Description}").GetAwaiter().GetResult();
    var _ticketDetails = triageResponse.Result;
    Console.WriteLine($"[Triage] Categorized as: {_ticketDetails}");
    // Update the ticket details with the category
    return _ticketDetails;
};

//Node 
var triageNode = triageWorkflow.BindAsExecutor("TriageNode");


Func<TicketDetails, TicketDetails> hardwareWorkflow = (TicketDetails ticketDetails) =>
{
    Console.WriteLine($"[Hardware] Processing ticket: {ticketDetails.Description}");
    // Use the hardware agent to provide a solution
    var hardwareResponse = hardwareAgent.RunAsync($"Provide a solution for the following hardware issue: {ticketDetails.Description}").GetAwaiter().GetResult();
    string solution = hardwareResponse.Text.Trim();
    // Update the ticket details with the solution
    return ticketDetails with { Description = solution };
};

var hardwareNode = hardwareWorkflow.BindAsExecutor("HardwareNode");

Func<TicketDetails, TicketDetails> softwareWorkflow = (TicketDetails ticketDetails) =>
{
    Console.WriteLine($"[Software] Processing ticket: {ticketDetails.Description}");
    // Use the software agent to provide a solution
    var softwareResponse = softwareAgent.RunAsync($"Provide a solution for the following software issue: {ticketDetails.Description}").GetAwaiter().GetResult();
    string solution = softwareResponse.Text.Trim();
    // Update the ticket details with the solution
    return ticketDetails with { Description = solution };
};

var softwareNode = softwareWorkflow.BindAsExecutor("SoftwareNode");

Func<TicketDetails,TicketDetails> OutOfContextWorkFlow = (TicketDetails ticketDetails) =>
{
    Console.WriteLine($"[OutOfContext] Processing ticket: {ticketDetails.Description}");
    return ticketDetails;
};

var outOfContextNode = OutOfContextWorkFlow.BindAsExecutor("OutOfContextNode");



var workflow = new WorkflowBuilder(triageNode)
    .AddEdge<TicketDetails>(triageNode, hardwareNode, condition: (ticket) => ticket?.Category == "Hardware")
    .AddEdge<TicketDetails>(triageNode, softwareNode, condition: (ticket) => ticket?.Category == "Software")
    .AddEdge<TicketDetails>(triageNode, outOfContextNode, condition: (ticket) => ticket?.OutOfContext == true)
    .Build();

Console.WriteLine("-- User Input --");
var userInput = Console.ReadLine() ?? throw new InvalidOperationException("User input is null.");

// 5. Execution of the workflow

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new TicketDetails(userInput, "", false));


TicketDetails? finalResult = null;

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorInvokedEvent started:
            Console.WriteLine($"[Workflow] Node started: {started.ExecutorId}");
            break;

        case ExecutorCompletedEvent completed:
            Console.WriteLine($"[Workflow] Node completed: {completed.ExecutorId} -> {completed.Data}");
            break;

        case ExecutorFailedEvent failed:
            Console.WriteLine($"[Workflow] Node FAILED: {failed.ExecutorId} -> {failed.Data}");
            break;

        case WorkflowOutputEvent output when output.Is<TicketDetails>():
            finalResult = output.As<TicketDetails>();
            break;

        case WorkflowErrorEvent error:
            Console.WriteLine($"[Workflow] Workflow error: {error.Exception}");
            break;
    }
}

Console.WriteLine("-- Flow completed Successfully --");
Console.WriteLine($"Final ticket: {finalResult}");
Console.ReadLine();


public record TicketDetails(
    [property: Description("Ticket description. Empty if out of context.")]
    string Description,
    [property: Description("Ticket category")]
    string Category,
    [property: Description("Set to true if the user's question is NOT about tickets and is therefore out of context; otherwise false.")]
    bool OutOfContext
);
