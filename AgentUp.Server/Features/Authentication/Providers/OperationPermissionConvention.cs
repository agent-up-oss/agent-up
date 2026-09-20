using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class OperationPermissionConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers.Where(controller =>
                     !controller.Attributes.OfType<IAllowAnonymous>().Any()))
        {
            foreach (var action in controller.Actions
                         .Where(candidate => !candidate.Attributes.OfType<IAllowAnonymous>().Any())
                         .Select(candidate => (Action: candidate, Permission: OperationPermissionMap.For(controller.ControllerName, candidate.ActionName)))
                         .Where(candidate => candidate.Permission is not null))
            {
                action.Action.Filters.Add(new AuthorizeFilter([new AuthorizeAttribute(action.Permission!)]));
            }
        }
    }
}
