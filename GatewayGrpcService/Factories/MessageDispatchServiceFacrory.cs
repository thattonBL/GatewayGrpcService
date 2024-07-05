namespace GatewayGrpcService.Factories;

public class MessageDispatchServiceFacrory : IMessageDispatchServiceFacrory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MessageDispatchServiceFacrory> _logger;
    private readonly IMessageDispatchService _messageDispatchService;


    public MessageDispatchServiceFacrory(IConfiguration configuration, IServiceProvider services, ILogger<MessageDispatchServiceFacrory> logger)
    {
        _configuration = configuration;
        _messageDispatchService = _configuration["MessageServices:GrpcEnabled"] == "true" ? (IMessageDispatchService)services.GetService(typeof(GrpcMessageDispatchService)) : (IMessageDispatchService)services.GetService(typeof(HttpMessageDispatchService));
        _logger = logger;
    }

    public IMessageDispatchService GetDispatchService()
    {
        return _messageDispatchService;
    }
}
