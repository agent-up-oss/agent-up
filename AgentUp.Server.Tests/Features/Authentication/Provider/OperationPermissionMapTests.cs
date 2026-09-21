using System.Reflection;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class OperationPermissionMapTests
{
    [Test]
    public void For_RequiresWorkspaceReadToListWorkspaces()
        => Assert.That(OperationPermissionMap.For("Workspaces", "GetAll"), Is.EqualTo(OperationPermissions.WorkspaceRead));

    [Test]
    public void For_RequiresWorkspaceCreateToRegisterAWorkspace()
        => Assert.That(OperationPermissionMap.For("Workspaces", "Register"), Is.EqualTo(OperationPermissions.WorkspaceCreate));

    [Test]
    public void EntitlementsStayAuthenticatedWithoutAPermission()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OperationPermissionMap.IsOpenAuthenticated("Entitlements", "Get"), Is.True);
            Assert.That(OperationPermissionMap.For("Entitlements", "Get"), Is.Null);
        });
    }

    [Test]
    public void EveryHttpActionIsMappedAnonymousOrOpenAuthenticated()
    {
        var missing = typeof(Program).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(HttpActions)
            .Where(action => !action.Anonymous
                             && !OperationPermissionMap.IsOpenAuthenticated(action.Controller, action.Action)
                             && OperationPermissionMap.For(action.Controller, action.Action) is null)
            .Select(action => $"{action.Controller}.{action.Action}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(missing, Is.Empty,
            "Map a permission for each new HTTP action, mark it AllowAnonymous, or add it to the open-authenticated set.");
    }

    private static IEnumerable<(string Controller, string Action, bool Anonymous)> HttpActions(Type controller)
    {
        var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controller.Name[..^"Controller".Length]
            : controller.Name;
        var controllerAnonymous = controller.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
        return controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes().OfType<IActionHttpMethodProvider>().Any())
            .Select(method => (
                controllerName,
                method.Name,
                controllerAnonymous || method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)));
    }
}
