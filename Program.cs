using System.Text.Json.Serialization;
using ClearBooksFYP.Models;
using ClearBooksFYP.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//register services with DI container
builder.Services.AddControllers();

//configure Entity Framework Core to use SQL Server
builder.Services.AddDbContext<ClearBooksDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ClearBooksDatabase")));

//add CORS policy 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policyBuilder =>
    {
        policyBuilder.WithOrigins(
            "https://localhost:5001",
            "https://localhost:7103",
            "https://localhost:5222",  
            "http://localhost:5222",   
            "http://clearbooksfyp.somee.com/",
            "http://www.clearbooksfyp.somee.com/"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials(); //allow credentials for session cookies
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

//add logging
builder.Services.AddLogging();

//add Swagger for API documentation (useful for testing)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


//use CORS first
app.UseCors("AllowSpecificOrigins");

//serve static files BEFORE authentication middleware
app.UseDefaultFiles(); // Serve index.html or default.html if present in wwwroot
app.UseStaticFiles();  // Enable serving static files like CSS, JS, etc.

//set up the middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//add authentication middleware AFTER static files but BEFORE authorization
app.UseAuthMiddleware();

app.UseAuthorization();

//map controllers for API endpoints
app.MapControllers();

//fallback route for SPA - serve appropriate HTML file
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value?.ToLower();

    // If it's an API request, return 404
    if (path?.StartsWith("/api/") == true)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("API endpoint not found");
        return;
    }

    // Check if user is authenticated by looking for session
    var sessionId = GetSessionIdFromContext(context);
    var isAuthenticated = false;

    if (!string.IsNullOrEmpty(sessionId))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClearBooksDbContext>();

        var session = await dbContext.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId &&
                                    s.ExpiresAt > DateTime.Now &&
                                    s.IsActive &&
                                    s.User.IsActive);

        isAuthenticated = session != null;
    }

    // Redirect based on authentication status
    context.Response.ContentType = "text/html";

    if (isAuthenticated)
    {
        // User is authenticated, serve the dashboard/main app
        await context.Response.SendFileAsync("wwwroot/index.html");
    }
    else
    {
        // User is not authenticated, serve login page
        await context.Response.SendFileAsync("wwwroot/login.html");
    }
});

// Helper method to get session ID
string GetSessionIdFromContext(HttpContext context)
{
    // Try Authorization header first
    if (context.Request.Headers.ContainsKey("Authorization"))
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            return authHeader.Substring("Bearer ".Length);
        }
    }

    // Try cookie
    if (context.Request.Cookies.ContainsKey("SessionId"))
    {
        return context.Request.Cookies["SessionId"];
    }

    return null;
}

app.Run();