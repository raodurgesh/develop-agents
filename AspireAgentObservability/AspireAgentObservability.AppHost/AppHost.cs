var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireAgentObservability_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithUrls(context =>
    {
        var baseUrl = context.Urls.FirstOrDefault();
        if (baseUrl is not null)
        {
            context.Urls.Add(new() { Url = baseUrl.Url.TrimEnd('/') + "/devui",
            DisplayText = "AspireAgentObservability DevUI"
            });
        }
    });


builder.Build().Run();
