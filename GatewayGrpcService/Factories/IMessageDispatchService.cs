using BL.Gateway.Events.Common;
using GatewayGrpcService.Models;

namespace GatewayGrpcService.Factories;

public interface IMessageDispatchService
{
    Task<RsiMessageRecievedDataModel> SendSingleRsiMessage<T>(T messageData);

    Task<IEnumerable<RsiMessageRecievedDataModel>> SendBulkRsiMessages<T>(IEnumerable<T> messageData);
}
