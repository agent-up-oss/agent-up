using Avalonia.Controls;
using LocalInstaller.App.Features.Installation.Controllers;
using LocalInstaller.App.Features.Installation.Services;
using LocalInstaller.App.Features.Installation.Views;
using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.App.Composition;

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
