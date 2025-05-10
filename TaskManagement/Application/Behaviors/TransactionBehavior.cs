
using MediatR; 
using Microsoft.EntityFrameworkCore; 
using Microsoft.Extensions.Logging; 
using System.Threading;
using System.Threading.Tasks;
using System; 


namespace TaskManagement.Application.MediatR
{
    // MediatR pipeline behavior for managing database transactions
    // This intercepts commands and wraps their handling in a transaction
    public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

        public TransactionBehavior(AppDbContext dbContext, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Only apply transaction to commands (requests that are not queries)
            // A common convention is to check if the request name ends with "Command"
            // or implement a marker interface like ICommand if you have one.
            var requestName = typeof(TRequest).Name;
            if (!requestName.EndsWith("Command"))
            {
                // If it's not a command, just pass it down the pipeline
                return await next();
            }

            // If it's a command, start a transaction
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogInformation("Beginning transaction for {RequestName}", requestName);

                // Execute the next step in the pipeline (usually the handler)
                var response = await next();

                // Commit the transaction if the handler succeeded
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Committed transaction for {RequestName}", requestName);

                return response;
            }
            catch (Exception ex)
            {
                // Rollback the transaction if an exception occurred
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Rolled back transaction for {RequestName} due to exception: {Message}", requestName, ex.Message);

                throw; // Re-throw the exception
            }
        }
    }
}
