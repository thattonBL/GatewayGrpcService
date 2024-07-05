namespace GatewayGrpcService.Factories
{
    public interface IMessageDispatchServiceFacrory
    {
        IMessageDispatchService GetDispatchService();
    }
}