using AzureDevOps.Automation.Function.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(AzureDevOps.Automation.Function.Startup))]

namespace AzureDevOps.Automation.Function;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var configuration = builder.GetContext().Configuration;
        var connectionString = configuration.GetValue<string>("AzureWebJobsStorage");

        builder.Services.AddAzureClients(x =>
        {
            x.AddBlobServiceClient(connectionString);
        });

        builder.Services.AddSingleton<IRuleService, RuleService>();
    }
}