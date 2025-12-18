using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;

namespace ClearBooksFYP.Controllers
{
    public class BaseController : ControllerBase
    {   
        // method that validates sessions and returns the current user id
        protected async Task<int> GetCurrentUserIdAsync(ClearBooksDbContext context)
        {
            //first try to get from HttpContext.Items 
            if (HttpContext.Items.ContainsKey("UserId"))
            {
                return (int)HttpContext.Items["UserId"];
            }

            //if not found, try to get session ID and validate manually
            var sessionId = GetSessionIdFromRequest();
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new UnauthorizedAccessException("No session found - user not authenticated");
            }

            //validate session and get user
            var session = await context.UserSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId &&
                                        s.ExpiresAt > DateTime.Now &&
                                        s.IsActive &&
                                        s.User.IsActive);

            if (session == null)
            {
                throw new UnauthorizedAccessException("Invalid or expired session");
            }

            //store in HttpContext.Items for future use in this request
            HttpContext.Items["UserId"] = session.UserId;
            HttpContext.Items["User"] = session.User;

            return session.UserId;
        }

        protected int GetCurrentUserId()
        {
            //the synchronous version that should only be used if UserId is already in context
            if (HttpContext.Items.ContainsKey("UserId"))
            {
                return (int)HttpContext.Items["UserId"];
            }
            throw new UnauthorizedAccessException("User not authenticated - use GetCurrentUserIdAsync instead");
        }

        protected async Task<User> GetCurrentUserAsync(ClearBooksDbContext context)
        {
            //first try to get from HttpContext.Item
            if (HttpContext.Items.ContainsKey("User"))
            {
                return (User)HttpContext.Items["User"];
            }

            //if not found, get user ID and fetch user
            var userId = await GetCurrentUserIdAsync(context);
            var user = await context.Users.FindAsync(userId);

            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

            //store in HttpContext.Items for future use in this request
            HttpContext.Items["User"] = user;
            return user;
        }

        protected User GetCurrentUser()
        {
            //synchronous version that should only be used if User is already in context
            if (HttpContext.Items.ContainsKey("User"))
            {
                return (User)HttpContext.Items["User"];
            }
            throw new UnauthorizedAccessException("User not authenticated - use GetCurrentUserAsync instead");
        }

        protected bool IsAuthenticated()
        {
            return HttpContext.Items.ContainsKey("UserId") && HttpContext.Items.ContainsKey("User");
        }

        private string GetSessionIdFromRequest()
        {
            //try to get session ID from Authorization header
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    return authHeader.Substring("Bearer ".Length);
                }
            }

            //try to get session ID from cookie
            if (Request.Cookies.ContainsKey("SessionId"))
            {
                return Request.Cookies["SessionId"];
            }

            return null;
        }
    }
}