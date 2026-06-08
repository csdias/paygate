using System.Diagnostics;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public class TraceDataEnricher : ITraceDataEnricher
{
    public bool EnrichAndUpdateMessage(OutboxMessage message)
    {
        var activity = new Activity("OutboxReserveMessage");
        var requiresUpdate = false;
        try
        {
            activity.Start();
            if (string.IsNullOrEmpty(message.TraceParent))
            {
                message.TraceParent = activity.Id;
                requiresUpdate = true;
            }
            else
            {
                activity.SetParentId(message.TraceParent);
            }

            if (string.IsNullOrEmpty(message.TraceState))
            {
                activity.TraceStateString = $"contextId={message.ContextId}";
                message.TraceState = activity.TraceStateString;
                requiresUpdate = true;
            }
        }
        finally
        {
            activity.Stop();
        }

        return requiresUpdate;
    }
}
