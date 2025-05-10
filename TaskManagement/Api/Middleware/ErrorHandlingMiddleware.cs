

using System.Net;
using System.Text.Json;
using Application.Exceptions;
using FluentValidation;

namespace Api.Middleware
{
    // Middleware for global error handling in the API pipeline.
   
    public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger) {
       
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await next(httpContext);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        // Handles the exception by setting the HTTP response status code and body.
        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            HttpStatusCode statusCode;
            var message = "An internal server error occurred. Please try again later."; 
            object errors = null;

           
            switch (exception)
            {
                case UnauthorizedAccessException _: 
                    statusCode = HttpStatusCode.Unauthorized; // 401 Unauthorized
                    message = "Authentication failed.";
                    break;
                case ForbiddenAccessException forbiddenEx: 
                    statusCode = HttpStatusCode.Forbidden; // 403 Forbidden
                    message = forbiddenEx.Message; 
                    break;
                case NotFoundException notFoundEx: 
                    statusCode = HttpStatusCode.NotFound; // 404 Not Found
                    message = notFoundEx.Message; 
                    break;
                case ConflictException conflictEx: 
                    statusCode = HttpStatusCode.Conflict; // 409 Conflict
                    message = conflictEx.Message; 
                    break;
                case ValidationException validationException: 
                    statusCode = HttpStatusCode.BadRequest; // 400 Bad Request
                    message = "Validation failed."; // Generic message for validation errors
                  
                    errors = validationException.Errors.Select(err => new {
                        err.PropertyName, err.ErrorMessage
                    }).ToList();
                    break;
                default:
                    statusCode = HttpStatusCode.InternalServerError; // 500 Internal Server Error
                    break;
            }
            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                statusCode = (int)statusCode,
                message,
                errors 
            };

        
            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
