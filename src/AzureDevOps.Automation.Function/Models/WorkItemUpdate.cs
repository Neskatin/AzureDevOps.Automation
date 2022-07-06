namespace AzureDevOps.Automation.Function.Models;
#nullable disable

public class WorkItemUpdate
{
    public string Organization { get; set; }
    public int WorkItemId { get; set; }
    public string WorkItemType { get; set; }
    public string State { get; set; }
    public string EventType { get; set; }
    public string TeamProject { get; set; }
}
