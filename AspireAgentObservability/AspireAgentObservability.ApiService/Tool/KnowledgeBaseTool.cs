using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AspireAgentObservability.ApiService.Tool;

/// <summary>
/// Tool the agent can call to look up information in the knowledge base.
/// Currently returns stubbed data; will be backed by a database later.
/// </summary>
public class KnowledgeBaseTool
{
    private readonly ILogger<KnowledgeBaseTool> _logger;

    // Constructor injection so we can add a DbContext / repository later
    // without changing the tool's public signature.
    public KnowledgeBaseTool(ILogger<KnowledgeBaseTool> logger)
    {
        _logger = logger;
    }

    [Description("Queries the knowledge base for information relevant to the user's question.")]
    public async Task<KnowledgeBaseResult> QueryKnowledgeBaseAsync(
        [Description("The natural-language query to search the knowledge base for.")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("KnowledgeBase query was empty.");
            return new KnowledgeBaseResult(
                Found: false,
                Answer: "No query was provided.",
                Source: null);
        }

        _logger.LogInformation("Querying knowledge base for: {Query}", query);

        // TODO: Replace this stub with a real database lookup, e.g.:
        //   var match = await _dbContext.Articles
        //       .Where(a => EF.Functions.Like(a.Content, $"%{query}%"))
        //       .FirstOrDefaultAsync(cancellationToken);
        //   return match is null
        //       ? new KnowledgeBaseResult(false, "No matching entry found.", null)
        //       : new KnowledgeBaseResult(true, match.Content, match.Title);

        await Task.CompletedTask; // placeholder for the async DB call

        return new KnowledgeBaseResult(
            Found: true,
            Answer: $"This is a sample response from the knowledge base for '{query}'.",
            Source: "stub-data");
    }
}

/// <summary>Structured result returned to the agent.</summary>
public record KnowledgeBaseResult(
    [property: Description("True if a relevant knowledge-base entry was found.")]
    bool Found,

    [property: Description("The answer text retrieved from the knowledge base.")]
    string Answer,

    [property: Description("Where the answer came from (e.g., article title), if any.")]
    string? Source);