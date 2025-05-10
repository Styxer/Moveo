using System.Linq.Expressions;

namespace Application.Interfaces 
{
    public interface IRepository<T> where T : class
    {
        Task<T>? GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        void Remove(T entity);
        Task<int> SaveChangesAsync();
        IQueryable<T> AsQueryable(); // For pagination/further filtering in services
    }
}