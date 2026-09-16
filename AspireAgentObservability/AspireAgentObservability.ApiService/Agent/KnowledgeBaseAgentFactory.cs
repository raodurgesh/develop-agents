using AspireAgentObservability.ApiService.Tool;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AspireAgentObservability.ApiService.Agent;

public static class KnowledgeBaseAgentFactory
{
    public const string AgentName = "KnowledgeBase Assistant";

    public static AIAgent Create(IServiceProvider sp, string name)
    {
        var kbTool = sp.GetRequiredService<KnowledgeBaseTool>();
        var chat = sp.GetRequiredService<IChatClient>();
        AIFunction kbFunction = AIFunctionFactory.Create(kbTool.QueryKnowledgeBaseAsync);

        return chat.AsAIAgent(new ChatClientAgentOptions
        {
            Name = name,
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a knowledge base assistant. You have access to a knowledge base that contains information about various topics. Your task is to answer questions and provide information based on the knowledge base. If you do not know the answer, you should respond with 'I don't know. You should take help of tool to fetch answers'",
                Tools = [kbFunction]
            }
        });
    }
}

