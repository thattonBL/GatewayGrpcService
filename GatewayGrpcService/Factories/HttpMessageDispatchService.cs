using Events.Common;
using GatewayGrpcService.Models;
using GatewayGrpcService.Queries;
using Newtonsoft.Json;
using System.Text;

namespace GatewayGrpcService.Factories;

public class HttpMessageDispatchService : IMessageDispatchService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpMessageDispatchService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpMessageDispatchService()
    {

    }

    public HttpMessageDispatchService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<HttpMessageDispatchService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IEnumerable<RsiMessageRecievedDataModel>> SendBulkRsiMessages<T>(IEnumerable<T> messageData)
    {
        var responseList = new List<RsiMessageRecievedDataModel>();
        await LoopAsync(messageData, responseList);
        return responseList;
    }

    public async Task<RsiMessageRecievedDataModel> SendSingleRsiMessage<T>(T messageData)
    {
        using (var httpClient = _httpClientFactory.CreateClient())
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, Path.Combine(_configuration["MessageServices:Building33MockApiUri"],"api/Message"));
                request.Content = new StringContent(JsonConvert.SerializeObject(messageData), Encoding.UTF8, "application/json");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseReturn = JsonConvert.DeserializeObject<RsiMessageRecievedDataModel>(responseContent);
                
                return responseReturn;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to RSI service");
                throw;
            }
        }
    }

    public Task AddToList(RsiMessageRecievedDataModel item, List<RsiMessageRecievedDataModel> responseList)
    {
        responseList.Add(item);
        return Task.CompletedTask;
    }

    public async Task LoopAsync<T>(IEnumerable<T> sentMessages, List<RsiMessageRecievedDataModel> responseList)
    {
        List<Task> listOfTasks = new List<Task>();

        using (var httpClient = _httpClientFactory.CreateClient())
        {
            foreach (var rawMessage in sentMessages)
            {
                try
                {
                    var message = rawMessage as RSIMessage;
                    var messageData = new RsiPostItem
                    {
                        CollectionCode = message.collection_code,
                        Shelfmark = message.shelfmark,
                        VolumeNumber = message.volume_number,
                        StorageLocationCode = message.storage_location_code,
                        Author = message.author,
                        Title = message.title,
                        PublicationDate = message.publication_date.ToShortDateString(),
                        PeriodicalDate = message.periodical_date.ToShortDateString(),
                        ArticleLine1 = message.article_line1,
                        ArticleLine2 = message.article_line2,
                        CatalogueRecordUrl = message.catalogue_record_url,
                        FurtherDetailsUrl = message.further_details_url,
                        DtRequired = message.dt_required,
                        Route = message.route,
                        ReadingRoomStaffArea = message.reading_room_staff_area,
                        SeatNumber = message.seat_number,
                        ReadingCategory = message.reading_category,
                        Identifier = message.identifier,
                        ReaderName = message.reader_name,
                        ReaderType = message.reader_type.ToString(),
                        OperatorInformation = message.operator_information,
                        ItemIdentity = message.item_identity
                    };
                    
                    var request = new HttpRequestMessage(HttpMethod.Post, Path.Combine(_configuration["MessageServices:Building33MockApiUri"], "api/Message"));
                    request.Content = new StringContent(JsonConvert.SerializeObject(messageData), Encoding.UTF8, "application/json");
                    var response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseReturn = JsonConvert.DeserializeObject<RsiMessageRecievedDataModel>(responseContent);


                    listOfTasks.Add(AddToList(responseReturn, responseList));
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error sending bulk message to RSI service");
                }               
            }
            await Task.WhenAll(listOfTasks);
        }
    }
}
