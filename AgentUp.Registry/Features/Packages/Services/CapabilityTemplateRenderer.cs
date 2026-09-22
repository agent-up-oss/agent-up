using System.Text.RegularExpressions;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Registry.Features.Packages.Services;

public sealed class CapabilityTemplateRenderer
{
    private static readonly Regex Token = new(@"\{\{(parameters|environment)\.([^}]+)\}\}", RegexOptions.Compiled);

    public string Render(string template, CapabilityTemplateValues values)
        => Token.Replace(template, match => Lookup(match.Groups[1].Value, match.Groups[2].Value, values));

    public IReadOnlyList<string> RenderAll(IReadOnlyList<string> templates, CapabilityTemplateValues values)
        => templates.Select(template => Render(template, values)).ToArray();

    private static string Lookup(string source, string key, CapabilityTemplateValues values)
    {
        var map = source == "parameters" ? values.Parameters : values.Environment;
        if (!map.TryGetValue(key, out var value))
            throw new InvalidOperationException($"Capability template token '{{{{ {source}.{key} }}}}' has no value.");
        return value;
    }
}
