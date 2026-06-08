using System.Data;

namespace Paygate.Domain;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
