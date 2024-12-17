using BL.Gateway.Events.Common;
using BL.Gateway.Events.Common.Events;

namespace GatewayGrpcService.IntegrationEvents.Events;
public record NewRsiMessageSubmittedIntegrationEvent : BaseRsiMessageSubmittedIntegrationEvent
{
    public NewRsiMessageSubmittedIntegrationEvent(RsiPostItem rsiMessage, string eventName)
        : base(rsiMessage, eventName)
    {

    }
}
