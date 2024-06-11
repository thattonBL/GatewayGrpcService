namespace GatewayGrpcService.Queries
{
    public interface IGatewayRequestQueries
    {
        Task<IEnumerable<RSIMessage>> GetRSIMessagesFromDbAsync();
        Task<IEnumerable<Common>> SetRsiMessagesToAckedAsync();
    }
}