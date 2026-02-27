using System.Net;
using Microsoft.AspNetCore.Components;

namespace Chop.Web.Auth;

public static class AuthErrorRedirector
{
    public static bool TryRedirect(Exception ex, WebAuthSession session, NavigationManager navigationManager)
    {
        if (ex is not HttpRequestException http || !http.StatusCode.HasValue)
        {
            return false;
        }

        if (http.StatusCode == HttpStatusCode.Unauthorized)
        {
            session.SignOut();
            navigationManager.NavigateTo("/login", replace: true);
            return true;
        }

        if (http.StatusCode == HttpStatusCode.Forbidden)
        {
            navigationManager.NavigateTo("/forbidden", replace: true);
            return true;
        }

        return false;
    }
}
