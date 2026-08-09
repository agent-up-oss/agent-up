using Avalonia.Controls;
using AgentUp.InstallerApp.Features.Installation.Controllers;
using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.InstallerApp.Features.Installation.Views;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Composition;

public static class AppComposition
{
    private static InstallerProductRegistration? _product;

    public static void ConfigureProduct(
        ProductManifest product,
        string? fakeInstallerVariable = null,
        string? nixOsLookupOnlyVariable = null)
        => _product = new InstallerProductRegistration(product, fakeInstallerVariable, nixOsLookupOnlyVariable);

    public static Window CreateInstallerWindow()
        => new InstallerWindow { DataContext = InstallerViewModelFactory.CreateDefault(_product) };

    public static InstallerCommandLineController CreateCommandLineController()
        => new InstallerCommandLineController(new InstallerCommandLineService());
}

public sealed record InstallerProductRegistration(
    ProductManifest Product,
    string? FakeInstallerVariable = null,
    string? NixOsLookupOnlyVariable = null);
