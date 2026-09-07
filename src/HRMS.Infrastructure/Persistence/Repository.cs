using System.Linq.Expressions;
using HRMS.Application.Common.Interfaces;
using HRMS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly HrmsDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(HrmsDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default) =>
        await _dbSet.ToListAsync(ct);

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _dbSet.Where(predicate).ToListAsync(ct);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _dbSet.AddAsync(entity, ct);

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity)
    {
        // Soft delete by convention instead of physically removing the row.
        entity.IsDeleted = true;
        _dbSet.Update(entity);
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly HrmsDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(HrmsDbContext context) => _context = context;

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repo))
        {
            repo = new Repository<T>(_context);
            _repositories[type] = repo;
        }
        return (IRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
