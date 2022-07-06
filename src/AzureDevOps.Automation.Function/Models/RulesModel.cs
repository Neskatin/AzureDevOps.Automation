using System;

namespace AzureDevOps.Automation.Function.Models;
#nullable disable

public class RulesModel
{
    public string Type { get; set; }

    public Rule[] Rules { get; set; } = Array.Empty<Rule>();
}

public class Rule
{
    public string[] IfChildState { get; set; }

    public string[] NotParentStates { get; set; }

    public string SetParentStateTo { get; set; }

    public bool AllChildren { get; set; }
}