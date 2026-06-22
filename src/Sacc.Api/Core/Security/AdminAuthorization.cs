using Microsoft.AspNetCore.Authorization;

namespace Sacc.Api.Core.Security;

/// <summary>Policy <c>require_admin</c>: exige role=admin; caso contrário 403 "Permissão insuficiente".</summary>
public static class AdminAuthorization
{
    public const string PolicyName = "require_admin";

    public static AuthorizationOptions AddRequireAdmin(this AuthorizationOptions options)
    {
        options.AddPolicy(PolicyName, policy =>
            policy.RequireAssertion(ctx => ctx.User.HasClaim("role", "admin")));
        return options;
    }
}
