using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using AzureDevOps.Automation.Function.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureDevOps.Automation.Function.Services;

public interface IRuleService
{
    Task<bool> HasRuleForTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<RulesModel?> GetRuleForTypeAsync(string type, CancellationToken cancellationToken = default);
}

public class RuleService : IRuleService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<RuleService> _logger;
    private readonly string _containerName;

    public RuleService(BlobServiceClient blobServiceClient, IConfiguration configuration, ILogger<RuleService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _containerName = configuration.GetValue<string>("RulesContainer");
    }

    public async Task<bool> HasRuleForTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        var filename = $"rule.{type.ToLower()}.json";
        var blob = _blobServiceClient
            .GetBlobContainerClient(_containerName)
            ?.GetBlobClient(filename);

        return blob != null && await blob.ExistsAsync(cancellationToken);
    }

    public async Task<RulesModel?> GetRuleForTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        try
        {
            var filename = $"rule.{type.ToLower()}.json";
            var blob = _blobServiceClient
                .GetBlobContainerClient(_containerName)
                ?.GetBlobClient(filename);

            if (blob == null || !await blob.ExistsAsync(cancellationToken))
            {
                _logger.NoRuleFound(filename, _containerName);
                return null;
            }

            var file = await blob.DownloadContentAsync(cancellationToken: cancellationToken);
            if (file == null)
            {
                _logger.NoRuleFound(filename, _containerName);
                return null;
            }

            return JsonSerializer.Deserialize<RulesModel>(file.Value.Content.ToString(),
                new JsonSerializerOptions {PropertyNameCaseInsensitive = true});
        }
        catch (Exception e)
        {
            _logger.ErrorLoadingRule(type, _containerName, e);
            return null;
        }
    }
}

public static partial class RuleServiceLoggerExtensions
{
    [LoggerMessage(EventId = 0,
        Level = LogLevel.Warning,
        Message = "No rule found for the filename \"{filename}\" in the container \"{containerName}\".")]
    public static partial void NoRuleFound(this ILogger<RuleService> logger, string filename, string containerName);

    [LoggerMessage(EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to load rules for {type} from container {containerName}.")]
    public static partial void ErrorLoadingRule(this ILogger<RuleService> logger, string type, string containerName, Exception ex);
}