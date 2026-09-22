using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.Packages.DTOs;
using AgentUp.Registry.Features.Packages.Services;

namespace AgentUp.Registry.Features.Packages.Controllers;

public sealed class CapabilityPackageController(
    CapabilityPackageValidator validator,
    CapabilityTemplateRenderer renderer)
{
    public CapabilityPackageValidationResult Validate(CapabilityPackageManifest? manifest)
        => validator.Validate(manifest);

    public string Render(string template, CapabilityTemplateValues values)
        => renderer.Render(template, values);

    public IReadOnlyList<string> RenderAll(IReadOnlyList<string> templates, CapabilityTemplateValues values)
        => renderer.RenderAll(templates, values);
}
