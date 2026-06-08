namespace Pay.Message.Exchange.OutboxPublisher.Db;

public class DatabaseOptions
{
    public int BatchSize { get; set; }
    public TimeSpan InactiveDelay { get; set; }
    public TimeSpan SleepDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public string TableName { get; set; }
    public string DatabaseName { get; set; }
    public int MaxConnectionPoolSize { get; set; } = 100;
}
