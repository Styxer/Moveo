using System.Linq.Expressions;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
namespace Infrastructure.Repository {
 
    public class Repository<T> : IRepository<T> where T : class
    {
       protected readonly DbContext Context;
       
        protected readonly DbSet<T> DbSet;
      
        private readonly ILogger<Repository<T>> _logger;
       
        private readonly AsyncRetryPolicy _retryPolicy;


       
        public Repository(DbContext context, ILogger<Repository<T>> logger)
        {
            Context = context;
            DbSet = context.Set<T>();
            _logger = logger;

     
            _retryPolicy = Policy
                .Handle<DbUpdateConcurrencyException>() 
                .Or<DbUpdateException>() 
                .Or<TimeoutException>() 
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Retry {RetryAttempt} encountered transient error. Waiting {TimeSpan} before retrying. Context: {Context}",
                            retryAttempt, timeSpan, context.OperationKey);
                    });
        }

        
        public async Task<T>? GetByIdAsync(Guid id)
        {
            
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogTrace("Executing GetByIdAsync for {EntityType} with ID {Id}", typeof(T).Name, id);
                return await DbSet.FindAsync(id);
            }) ?? throw new InvalidOperationException();
        }

   
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogTrace("Executing GetAllAsync for {EntityType}", typeof(T).Name);
                return await DbSet.ToListAsync();
            });
        }


        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            _logger.LogTrace("Building Find query for {EntityType}", typeof(T).Name);
            return DbSet.Where(predicate);
        }

        
        public async Task AddAsync(T entity)
        {
            _logger.LogTrace("Adding entity {EntityType}", typeof(T).Name);
            await DbSet.AddAsync(entity);
        }

       
        public void Remove(T entity)
        {
            _logger.LogTrace("Removing entity {EntityType}", typeof(T).Name);
            DbSet.Remove(entity);
           
        }

      
        public async Task<int> SaveChangesAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogTrace("Executing SaveChangesAsync");
                return await Context.SaveChangesAsync();
            });
        }

        public IQueryable<T> AsQueryable()
        {
            _logger.LogTrace("Returning AsQueryable for {EntityType}", typeof(T).Name);
            return DbSet.AsQueryable();
        }
    }
}
