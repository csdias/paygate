using System.Text.Json;
using AutoMapper;

namespace Pay.Message.Exchange.OutboxPublisher.Sns;

public class JsonObjectConverter : IValueConverter<string, object>
{
    public object Convert(string sourceMember, ResolutionContext context)
        => JsonSerializer.Deserialize<object>(sourceMember);
}
