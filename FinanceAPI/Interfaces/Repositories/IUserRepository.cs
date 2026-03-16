using System.Data;
using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(User user, IDbConnection conn, IDbTransaction txn);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task UpdatePasswordAsync(int id, string passwordHash, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
}
