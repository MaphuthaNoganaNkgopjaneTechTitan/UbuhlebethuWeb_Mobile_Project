using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;

namespace UbuhlebethuConnectPro.WebApp.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during the request. Path: {Path}", httpContext.Request.Path);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            // If it's an API request, return JSON
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                var result = JsonSerializer.Serialize(new { error = "An unexpected error occurred. Please try again later." });
                return context.Response.WriteAsync(result);
            }

            // Handle 401 Unauthorized API calls caused by expired tokens or lost sessions
            if (exception is System.Net.Http.HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.Unauthorized)
            {
                context.Response.Redirect("/Account/Login");
                return Task.CompletedTask;
            }
            
            // For MVC web app, redirect to generic error page
            context.Response.Redirect("/Account/Error");
            return Task.CompletedTask;
        }
    }
}
