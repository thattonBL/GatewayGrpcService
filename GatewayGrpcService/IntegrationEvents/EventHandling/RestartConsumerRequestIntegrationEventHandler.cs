using EventBus.Abstractions;
using GatewayGrpcService.IntegrationEvents.Events;
using GatewayGrpcService.Models;
using GatewayGrpcService.Queries;
using GatewayGrpcService.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GatewayGrpcService.IntegrationEvents.EventHandling;

public class RestartConsumerRequestIntegrationEventHandler : IIntegrationEventHandler<RestartConsumerRequestIntegrationEvent>
{
    private readonly IMessageServiceControl _messageServiceControl;
    private readonly IEventBus _eventBus;
    private readonly ILogger _logger;
    private readonly IGatewayRequestQueries _gatewayRequestQueries;
    private readonly GrpcMessageService _grpcMessageService;

    public RestartConsumerRequestIntegrationEventHandler(GrpcMessageService grpcMessageService,  IGatewayRequestQueries gatewayRequestQueries, IMessageServiceControl messageServiceControl, IEventBus eventBus, ILogger<RestartConsumerRequestIntegrationEventHandler> logger)
{
        _messageServiceControl = messageServiceControl;
        _eventBus = eventBus;
        _logger = logger;
        _gatewayRequestQueries = gatewayRequestQueries;
        _grpcMessageService = grpcMessageService;
    }
    public async Task Handle(RestartConsumerRequestIntegrationEvent @event)
    {
        _logger.LogInformation("Restarting GRPC Service: {@event}.", @event);
        try
        {
            var messages = await _gatewayRequestQueries.GetRSIMessagesFromDbAsync();
            var responseList = await _grpcMessageService.SendBulkRsiMessages(messages);
            //aynsc loop to publish event for every item
            await LoopAsync(responseList);
        }
        catch(Exception ex)
        {
            _logger.LogError("Error dispatching bulk messages after Grpc Service Restart message: {@message} for excaption {@exception}", ex.Message, ex);
        }

        try
        {
            //now delete or flag as sent
            var ackedMessages = await _gatewayRequestQueries.SetRsiMessagesToAckedAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError("Error setting archived messages to acknowledged after Grpc Service restart. Exception message: {@message}. For exception: {@ex}", ex.Message, ex);
        }
        //now set the status of the service
        _messageServiceControl.messageDeliveryPaused = false;
    }

    private Task DispatchPublishedEvent(RsiMessageRecievedDataModel item)
    {
        var newRsiPublishedEvent = new RsiMessagePublishedIntegrationEvent(item.ItemIdentity, RsiMessagePublishedIntegrationEvent.EVENT_NAME, "GatewayGrpcService");
        _eventBus.Publish(newRsiPublishedEvent);
        return Task.CompletedTask;
    }

    private async Task LoopAsync(IEnumerable<RsiMessageRecievedDataModel> sentMessages)
    {
        List<Task> listOfTasks = new List<Task>();

        foreach (var message in sentMessages)
        {
            listOfTasks.Add(DispatchPublishedEvent(message));
        }

        await Task.WhenAll(listOfTasks);
    }
}
