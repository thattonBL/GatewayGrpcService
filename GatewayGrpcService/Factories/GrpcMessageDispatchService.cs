﻿using BL.Gateway.Events.Common;
using GatewayGrpcService.Models;
using GatewayGrpcService.Protos;
using GatewayGrpcService.Queries;
using System.Globalization;
namespace GatewayGrpcService.Factories
{
    public class GrpcMessageDispatchService : GatewayGrpcMessagingService.GatewayGrpcMessagingServiceBase, IMessageDispatchService
    {
        private readonly GatewayGrpcMessagingService.GatewayGrpcMessagingServiceClient _gatewayMessagingClient;
        private readonly IGatewayRequestQueries _gatewayRequestQueries;

        public GrpcMessageDispatchService()
        {

        }

        public GrpcMessageDispatchService(GatewayGrpcMessagingService.GatewayGrpcMessagingServiceClient gatewayMessagingClient, IGatewayRequestQueries gatewayRequestQueries)
        {
            _gatewayMessagingClient = gatewayMessagingClient;
            _gatewayRequestQueries = gatewayRequestQueries;
        }

        public async Task<RsiMessageRecievedDataModel> SendSingleRsiMessage<T>(T messageData)
        {
            var request = new Protos.RSIMessage();
            var rawMessageData = messageData as RsiPostItem;
            request.CollectionCode = rawMessageData.CollectionCode;
            request.Shelfmark = rawMessageData.Shelfmark;
            request.VolumeNumber = rawMessageData.VolumeNumber;
            request.StorageLocationCode = rawMessageData.StorageLocationCode;
            request.Author = rawMessageData.Author;
            request.Title = rawMessageData.Title;

            var publicationDate = DateTime.ParseExact(rawMessageData.PublicationDate, "dd-MM-yyyy", CultureInfo.InvariantCulture);
            request.PublicationDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(publicationDate.ToUniversalTime());
            var periodicalDate = DateTime.ParseExact(rawMessageData.PeriodicalDate, "dd-MM-yyyy", CultureInfo.InvariantCulture);
            request.PeriodicalDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(periodicalDate.ToUniversalTime());

            request.ArticleLine1 = rawMessageData.ArticleLine1;
            request.ArticleLine2 = rawMessageData.ArticleLine2;
            request.CatalogueRecordUrl = rawMessageData.CatalogueRecordUrl;
            request.FurtherDetailsUrl = rawMessageData.FurtherDetailsUrl;
            request.DtRequired = rawMessageData.DtRequired != null ? rawMessageData.DtRequired : "";
            request.Route = rawMessageData.Route;
            request.ReadingRoomStaffArea = rawMessageData.ReadingRoomStaffArea != null ? rawMessageData.ReadingRoomStaffArea : "";
            request.SeatNumber = rawMessageData.SeatNumber;
            request.ReadingCategory = rawMessageData.ReadingCategory;
            request.Identifier = rawMessageData.Identifier;
            request.ReaderName = rawMessageData.ReaderName;
            request.ReaderType = Int32.Parse(rawMessageData.ReaderType);
            request.OperatorInformation = rawMessageData.OperatorInformation;
            request.ItemIdentity = rawMessageData.ItemIdentity;
            var response = await _gatewayMessagingClient.CreateStorageItemRequestAsync(request);
            //Publish Mesaage Sent Integration Event
            return new RsiMessageRecievedDataModel
            {
                ItemIdentity = response.ItemIdentity
            };
        }

        public async Task<IEnumerable<RsiMessageRecievedDataModel>> SendBulkRsiMessages<T>(IEnumerable<T> messageData)
        {
            var responseList = new List<RsiMessageRecievedDataModel>();
            await LoopAsync(messageData, responseList);
            return responseList;
        }

        public Task AddToList(RsiMessageRecievedDataModel item, List<RsiMessageRecievedDataModel> responseList)
        {
            responseList.Add(item);
            return Task.CompletedTask;
        }

        public async Task LoopAsync<T>(IEnumerable<T> sentMessages, List<RsiMessageRecievedDataModel> responseList)
        {
            List<Task> listOfTasks = new List<Task>();

            foreach (var rawMessage in sentMessages)
            {
                var responseMessage = new Protos.RSIMessage();
                var message = rawMessage as Queries.RSIMessage;
                responseMessage.Id = message.id;
                responseMessage.CollectionCode = message.collection_code;
                responseMessage.Shelfmark = message.shelfmark;
                responseMessage.VolumeNumber = message.volume_number;
                responseMessage.StorageLocationCode = message.storage_location_code;
                responseMessage.Author = message.author;
                responseMessage.Title = message.title;
                responseMessage.PublicationDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(message.publication_date.ToUniversalTime());
                responseMessage.PeriodicalDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(message.periodical_date.ToUniversalTime());
                responseMessage.ArticleLine1 = message.article_line1;
                responseMessage.ArticleLine2 = message.article_line2;
                responseMessage.CatalogueRecordUrl = message.catalogue_record_url;
                responseMessage.FurtherDetailsUrl = message.further_details_url;
                responseMessage.DtRequired = message.dt_required != null ? message.dt_required : "";
                responseMessage.Route = message.route;
                responseMessage.ReadingRoomStaffArea = message.reading_room_staff_area != null ? message.reading_room_staff_area : "";
                responseMessage.SeatNumber = message.seat_number;
                responseMessage.ReadingCategory = message.reading_category;
                responseMessage.Identifier = message.identifier;
                responseMessage.ReaderName = message.reader_name;
                responseMessage.ReaderType = message.reader_type;
                responseMessage.OperatorInformation = message.operator_information;
                responseMessage.ItemIdentity = message.item_identity;

                var response = await _gatewayMessagingClient.CreateStorageItemRequestAsync(responseMessage);
                //Publish Mesaage Sent Integration Event
                listOfTasks.Add(AddToList(new RsiMessageRecievedDataModel { ItemIdentity = response.ItemIdentity }, responseList));
            }

            await Task.WhenAll(listOfTasks);
        }
    }
}
