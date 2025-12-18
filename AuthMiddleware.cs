using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;

namespace ClearBooksFYP.Middleware
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthMiddleware> _logger;

        public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ClearBooksDbContext dbContext)
        {
            var path = context.Request.Path.Value?.ToLower();

            //skip authentication for public endpoints, static files, and HTML pages
            if (IsPublicEndpoint(path))
            {
                await _next(context);
                return;
            }

            try
            {
                var sessionId = GetSessionId(context);

                if (string.IsNullOrEmpty(sessionId))
                {
                    _logger.LogWarning("No session ID found for request to {Path}", path);
                    await WriteUnauthorizedResponse(context, "No session provided");
                    return;
                }

                // validate session
                var session = await dbContext.UserSessions
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId &&
                                            s.ExpiresAt > DateTime.Now &&
                                            s.IsActive &&
                                            s.User.IsActive);

                if (session == null)
                {
                    _logger.LogWarning("Invalid or expired session: {SessionId} for path {Path}", sessionId, path);
                    await WriteUnauthorizedResponse(context, "Invalid or expired session");
                    return;
                }

                //add user information to context
                context.Items["UserId"] = session.UserId;
                context.Items["User"] = session.User;

                _logger.LogDebug("Authentication successful for user {UserId} accessing {Path}", session.UserId, path);

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in authentication middleware for path {Path}", path);
                await WriteUnauthorizedResponse(context, "Authentication error");
            }
        }

        private bool IsPublicEndpoint(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            var publicPaths = new[]
            {
                //auth endpoints
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/validate",
                "/api/auth/logout",
                
                //static files
                "/css/",
                "/js/",
                "/scripts/",
                "/images/",
                "/favicon.ico",
                
                //hTML pages (allow all HTML files to be served)
                ".html",
                ".htm",
                
                //root
                "/"
            };

            //check if it's a static file extension
            var staticExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".eot" };
            if (staticExtensions.Any(ext => path.EndsWith(ext)))
            {
                return true;
            }

            //check against public paths
            return publicPaths.Any(publicPath => path.StartsWith(publicPath) || path.Contains(publicPath));
        }

        private string GetSessionId(HttpContext context)
        {
            //try Authorization header first
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    return authHeader.Substring("Bearer ".Length);
                }
            }

            //try cookie
            if (context.Request.Cookies.ContainsKey("SessionId"))
            {
                return context.Request.Cookies["SessionId"];
            }

            return null;
        }

        private async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var response = new { message, success = false };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    }

    //extension method to register the middleware
    public static class AuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthMiddleware>();
        }
    }
}