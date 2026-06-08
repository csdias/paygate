using System.Data;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public interface IDbConnectionFactory
{
    IDbConnection GetConnection();
}
