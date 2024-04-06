// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.

using System.Net;
using System.Security.Principal;
using System.Text;

namespace AppBackend
{
    public class AuthenticationTestMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthenticationTestMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();

            if (authorizationHeader != null && authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encodedCredentials = authorizationHeader["Basic ".Length..];
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                var separatorIndex = credentials.IndexOf(':');

                if (separatorIndex >= 0)
                {
                    var user = credentials[..separatorIndex];
                    var password = credentials[(separatorIndex + 1)..];

                    if (VerifyUserAndPassword(user, password))
                    {
                        var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user) };
                        var identity = new GenericIdentity(user);
                        var principal = new GenericPrincipal(identity, null);
                        context.User = principal;
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        return;
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            await _next(context);
        }

        private static bool VerifyUserAndPassword(string user, string password)
        {
            // This is not a real authentication scheme.
            return user == password;
        }
    }
}
