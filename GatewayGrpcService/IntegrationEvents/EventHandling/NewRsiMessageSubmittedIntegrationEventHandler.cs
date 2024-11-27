using EventBus.Abstractions;
using Events.Common.Events;
using GatewayGrpcService.Data;
using GatewayGrpcService.Factories;
using GatewayGrpcService.IntegrationEvents.Events;
using GatewayGrpcService.Services;
using System.Globalization;

namespace GatewayGrpcService.IntegrationEvents.EventHandling
{
    public class NewRsiMessageSubmittedIntegrationEventHandler : IIntegrationEventHandler<NewRsiMessageSubmittedIntegrationEvent>
    {
        private readonly ILogger<NewRsiMessageSubmittedIntegrationEventHandler> _logger;
        private readonly IEventBus _eventBus;
        private readonly ISQLMessageServices _sqlMessageServices;
        private readonly IMessageDispatchService _messageDispatchService;
        private readonly IMessageServiceControl _messageServiceControl;
        
        public NewRsiMessageSubmittedIntegrationEventHandler(IMessageDispatchServiceFacrory messageDispatchFactory, ISQLMessageServices sqlMessageService, IMessageServiceControl messageServiceControl, IEventBus eventBus, ILogger<NewRsiMessageSubmittedIntegrationEventHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _sqlMessageServices = sqlMessageService ?? throw new ArgumentNullException(nameof(sqlMessageService));
            _messageDispatchService = messageDispatchFactory.GetDispatchService();
            _messageServiceControl = messageServiceControl ?? throw new ArgumentNullException(nameof(messageServiceControl));
        }

        //TODO: We need to wrap this in a Behaviour as per the CQRS pattern in the GatewyRequestAPI
        public async Task Handle(NewRsiMessageSubmittedIntegrationEvent @event)
        {
            
            var newRsiMessageRecievedEvent = new NewRsiMessageRecievedIntegrationEvent(@event.RsiMessage.Identifier, NewRsiMessageRecievedIntegrationEvent.EVENT_NAME, "GatewayGrpcService");
            // add event to Redis Cache maybe?
            await Task.Run( () => _eventBus.Publish(newRsiMessageRecievedEvent));
            //Console.WriteLine("We've DOne it! We've sent the message to the Global Integration API!");
            var rawRsi = @event.RsiMessage;
            //check to see if the servuce is paused
            if (!_messageServiceControl.messageDeliveryPaused)
            {
                _logger.LogInformation("GRPC Service Active delivering message to building: {@rsi} with Identifier {@id}", @event.RsiMessage, @event.RsiMessage.Identifier);
                try
                {
                    //if it isn't then send the message using the data from the event
                    await _messageDispatchService.SendSingleRsiMessage(@event.RsiMessage);
                }
                catch(Exception ex)
                {
                    _logger.LogError("Error sending message to building: {@ex}", ex);
                }
                // Let the global integration api know that the message ghas been published
                var newRsiPublishedEvent = new RsiMessagePublishedIntegrationEvent(@event.RsiMessage.Identifier, RsiMessagePublishedIntegrationEvent.EVENT_NAME, "GatewayGrpcService");
                await Task.Run(() => _eventBus.Publish(newRsiPublishedEvent));
            }
            else
            {
                _logger.LogInformation("GRPC Service Paused archiving message: {@rsi} with Identifier {@id}", @event.RsiMessage, @event.RsiMessage.Identifier);

                try {
                    var rsiPoco = new RSI(rawRsi.CollectionCode, rawRsi.Shelfmark, rawRsi.VolumeNumber, rawRsi.StorageLocationCode, rawRsi.Author,
                                        rawRsi.Title, DateTime.ParseExact(rawRsi.PublicationDate, "dd-MM-yyyy", CultureInfo.InvariantCulture),
                                            DateTime.ParseExact(rawRsi.PeriodicalDate, "dd-MM-yyyy", CultureInfo.InvariantCulture), rawRsi.ArticleLine1, rawRsi.ArticleLine2, rawRsi.CatalogueRecordUrl, rawRsi.FurtherDetailsUrl,
                                                rawRsi.DtRequired, rawRsi.Route, rawRsi.ReadingRoomStaffArea, rawRsi.SeatNumber, rawRsi.ReadingCategory, rawRsi.Identifier,
                                                    rawRsi.ReaderName, Int32.Parse(rawRsi.ReaderType), rawRsi.OperatorInformation, rawRsi.ItemIdentity);
                    
                    //We need to put an execution strategy around this with retries do deal with Azure flakiness 
                    await _sqlMessageServices.AddNewRsiMessage(rsiPoco);
                }
                catch(Exception ex)
                {
                    _logger.LogError("Error adding message to SQL Database: {@ex}", ex);
                }               
            }
        }
    }   
}
