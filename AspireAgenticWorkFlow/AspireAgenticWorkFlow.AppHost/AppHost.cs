var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireAgenticWorkFlow_ApiService>("apiservice")
                 .WithUrls(context =>
                 {
                     var baseUrl = context.Urls.FirstOrDefault();
                     if (baseUrl is not null)
                     {
                         context.Urls.Add(new()
                         {
                             Url = baseUrl.Url.TrimEnd('/') + "/devui",
                             DisplayText = "Dev UI"
                         });
                     }
                 });
builder.Build().Run();


