using System.Data;
using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<bool> AnyAsync();
    Task<int> CreateAsync(User user);
    Task<int> CreateAsync(User user, IDbConnection conn, IDbTransaction txn);
    Task UpdateAsync(User user);
    Task UpdatePasswordAsync(int id, string passwordHash);
    Task SetActiveAsync(int id, bool isActive);
    Task DeleteAsync(int id);
    Task<int> CountActiveAdminsAsync();
}
