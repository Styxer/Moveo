using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors
{  
    public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

            try
            {
                var response = await next(cancellationToken);

                logger.LogInformation("Handled {RequestName} {@Response}", requestName, response);

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Request {RequestName} failed with exception: {Message}", requestName, ex.Message);
                throw; // Re-throw the exception so it can be handled by subsequent middleware (like error handling)
            }
        }
    }
}