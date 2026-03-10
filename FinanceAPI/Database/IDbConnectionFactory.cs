using System.Data;

namespace FinanceAPI.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
