using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AzureDevOps.Automation.Function.Extensions;
using AzureDevOps.Automation.Function.Models;
using AzureDevOps.Automation.Function.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace AzureDevOps.Automation.Function.Functions;

public class WorkItemUpdateFunction
{
    private readonly IRuleService _ruleService;
    private readonly string _pat;

    public WorkItemUpdateFunction(IConfiguration configuration, IRuleService ruleService)
    {
        _ruleService = ruleService;
        _pat = configuration.GetValue<string>("PAT");
    }

    [FunctionName(nameof(WorkItemUpdateFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhook/workitem/update")]
        HttpRequest req,
        ILogger log)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        dynamic? data = JsonConvert.DeserializeObject(requestBody);

        if (data == null)
            return new BadRequestErrorMessageResult("The request body couldn't be serialized.");

        WorkItemUpdate model = MapToModel(data);

        if (model.EventType != "workitem.updated" || model.WorkItemId <= 0 || string.IsNullOrWhiteSpace(model.WorkItemType))
            return new BadRequestErrorMessageResult("The sent event are not from the correct type or hasn't a valid id or work item.");

        if (!await _ruleService.HasRuleForTypeAsync(model.WorkItemType, req.HttpContext.RequestAborted))
            return new OkObjectResult($"No rule are found for the type {model.WorkItemType}.");

        var baseUri = new Uri("https://dev.azure.com/" + model.Organization);
        var connection = new VssConnection(baseUri,
            new VssCredentials(new VssBasicCredential(string.Empty, _pat)));
        using var client = connection.GetClient<WorkItemTrackingHttpClient>();

        var workItem = await client.GetWorkItemByIdAsync(model.WorkItemId, cancellationToken: req.HttpContext.RequestAborted);
        var parentRelation = workItem?.Relations.FirstOrDefault(x => x.Rel.Equals("System.LinkTypes.Hierarchy-Reverse"));

        if (parentRelation == null)
            return new OkObjectResult("No parent relation could be found.");

        var parentWorkItem =
            await client.GetWorkItemParentFromUrlAsync(parentRelation.Url, cancellationToken: req.HttpContext.RequestAborted);

        if (parentWorkItem == null)
            return new OkObjectResult("Parent work item couldn't be found.");

        var parentState = parentWorkItem.Fields["System.State"] == null
            ? string.Empty
            : parentWorkItem.Fields["System.State"].ToString();

        var rulesModel = await _ruleService.GetRuleForTypeAsync(model.WorkItemType, req.HttpContext.RequestAborted);

        if (rulesModel == null)
            return new OkObjectResult($"No rule are found for the type {model.WorkItemType}.");

        foreach (var rule in rulesModel.Rules)
        {
            if (!rule.IfChildState.Contains(model.State))
                continue;
            if (!rule.AllChildren)
            {
                if (rule.NotParentStates.Contains(parentState))
                    continue;

                await client.UpdateWorkItemStateAsync(parentWorkItem,
                    rule.SetParentStateTo,
                    cancellationToken: req.HttpContext.RequestAborted);
                return new OkObjectResult("Rule was applied for the item.");
            }

            var childWorkItems =
                await client.ListChildWorkItemsForParentAsync(parentWorkItem, cancellationToken: req.HttpContext.RequestAborted);
            var count = childWorkItems.Where(x => !rule.IfChildState.Contains(x.Fields["System.State"].ToString())).ToList().Count;

            if (count.Equals(0))
            {
                await client.UpdateWorkItemStateAsync(parentWorkItem,
                    rule.SetParentStateTo,
                    cancellationToken: req.HttpContext.RequestAborted);
            }

            return new OkObjectResult("Rule was applied for the item.");
        }

        return new OkObjectResult("No rule was applied for this change.");
    }

    private static WorkItemUpdate MapToModel(dynamic body) =>
        new()
        {
            Organization = GetOrganization(body.resource.url?.ToString()),
            WorkItemId = body.resource.workItemId == null ? -1 : Convert.ToInt32(body.resource.workItemId.ToString()),
            WorkItemType = body.resource.revision.fields["System.WorkItemType"]?.ToString(),
            State = body.resource.fields["System.State"].newValue?.ToString(),
            EventType = body.eventType?.ToString(),
            TeamProject = body.resource.fields["System.AreaPath"]?.ToString(),
        };

    private static string GetOrganization(string? url)
    {
        if (url == null)
            return string.Empty;

        url = url.Replace("http://", string.Empty);
        url = url.Replace("https://", string.Empty);

        if (url.Contains(value: "visualstudio.com"))
        {
            var split = url.Split('.');
            return split[0];
        }

        if (url.Contains("dev.azure.com"))
        {
            url = url.Replace("dev.azure.com/", string.Empty);
            var split = url.Split('/');
            return split[0];
        }

        return string.Empty;
    }
}