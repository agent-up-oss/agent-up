using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

public interface IProductScreenCatalog
{
    /// <summary>Every page-assembly screen for a surface, in the order its route must run.</summary>
    IReadOnlyList<ProductScreenDto> Screens(string surface);

    IReadOnlyList<string> Surfaces { get; }
}
