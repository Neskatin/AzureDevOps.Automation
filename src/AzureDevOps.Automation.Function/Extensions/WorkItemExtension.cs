using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AzureDevOps.Automation.Function.Extensions;

public static class WorkItemExtension
{
    public static int GetWorkItemIdFromUrl(string url)
    {
        var lastIndexOf = url.LastIndexOf("/", StringComparison.Ordinal);
        var size = url.Length - (lastIndexOf + 1);
        var value = url.Substring(lastIndexOf + 1, size);
        return Convert.ToInt32(value);
    }

    public static async Task<WorkItem?> GetWorkItemParentFromUrlAsync(
        this WorkItemTrackingHttpClient client,
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetWorkItemAsync(GetWorkItemIdFromUrl(url),
                null,
                null,
                WorkItemExpand.Relations,
                cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<WorkItem?> GetWorkItemByIdAsync(
        this WorkItemTrackingHttpClient client,
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetWorkItemAsync(id, null, null, WorkItemExpand.Relations, cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<List<WorkItem>> ListChildWorkItemsForParentAsync(
        this WorkItemTrackingHttpClient client,
        WorkItem parentWorkItem,
        CancellationToken cancellationToken = default)
    {
        var list = new List<WorkItem>();
        var children = parentWorkItem.Relations.Where<WorkItemRelation>(x => x.Rel.Equals("System.LinkTypes.Hierarchy-Forward"));

        IList<int> ids = children.Select(child => GetWorkItemIdFromUrl(child.Url)).ToList();

        return await client.GetWorkItemsAsync(ids, new[] {"System.State"}, cancellationToken: cancellationToken);
    }

    public static async Task<WorkItem?> UpdateWorkItemStateAsync(
        this WorkItemTrackingHttpClient client,
        WorkItem workItem,
        string state,
        CancellationToken cancellationToken = default)
    {
        var patchDocument = new JsonPatchDocument
        {
            new()
            {
                Operation = Operation.Test,
                Path = "/rev",
                Value = workItem.Rev.ToString()
            },
            new()
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = state
            }
        };

        try
        {
            return await client.UpdateWorkItemAsync(patchDocument, Convert.ToInt32(workItem.Id), cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }
}