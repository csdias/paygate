using AutoMapper;
using Pay.Message.Exchange.OutboxPublisher.Entities;
using Pay.Message.Exchange.OutboxPublisher.Sns.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Sns;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Build Metadata with a resolver function rather than a resolver type:
        // AutoMapper would otherwise have to instantiate the type via DI, which
        // fails for a non-registered (or non-public) resolver.
        CreateMap<OutboxMessage, SnsMessage>()
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom((src, _, _, _) => BuildMetadata(src)))
            .ForMember(dest => dest.Payload,
                opt => opt.ConvertUsing(new JsonObjectConverter(), src => src.MessageBody));
    }

    private static Metadata BuildMetadata(OutboxMessage src) =>
        new()
        {
            MessageType = src.MessageRegistry.MessageTypeId,
            MessageId = src.MessageId,
            MessageName = src.MessageRegistry.MessageName,
            MessageVersion = src.MessageRegistry.MessageVersion,
            ContextId = src.ContextId,
            OccurredAt = src.OccurredAt,
            Topic = src.MessageRegistry.Topic,
            ProcessedBy = src.ProcessedBy,
            PredecessorId = src.PredecessorId,
            TraceParent = src.TraceParent,
            TraceState = src.TraceState
        };
}
