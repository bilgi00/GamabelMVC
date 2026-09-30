using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace gamabelmvc.Services;

public sealed record ModuleRequirement(string Module) : IAuthorizationRequirement;
public sealed record RoleRequirement(params string[] Roles) : IAuthorizationRequirement;
public sealed record MenuRequirement(string MenuClaim, params string[] Roles) : IAuthorizationRequirement;

public sealed class AppAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement)
    {
        switch (requirement)
        {
            case ModuleRequirement module when string.Equals(
                context.User.FindFirst("app_module")?.Value, module.Module, StringComparison.OrdinalIgnoreCase):
                context.Succeed(requirement);
                break;

            case RoleRequirement role when role.Roles.Any(allowedRole =>
                context.User.FindAll(ClaimTypes.Role).Any(userRole =>
                    string.Equals(userRole.Value, allowedRole, StringComparison.OrdinalIgnoreCase))):
                context.Succeed(requirement);
                break;

            case MenuRequirement menu when context.User.IsInRole("admin")
                || menu.Roles.Any(allowedRole => context.User.FindAll(ClaimTypes.Role).Any(userRole =>
                    string.Equals(userRole.Value, allowedRole, StringComparison.OrdinalIgnoreCase)))
                || context.User.HasClaim(menu.MenuClaim, "1"):
                context.Succeed(requirement);
                break;
        }

        return Task.CompletedTask;
    }
}