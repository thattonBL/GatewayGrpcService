using Dapper;
using System.Data.SqlClient;
using System.Data;
using Polly;
using Polly.Retry;

namespace GatewayGrpcService.Queries
{
    public class GatewayRequestQueries : IGatewayRequestQueries
    {
        private string _connectionString = string.Empty;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ILogger<GatewayRequestQueries> _logger;

        public GatewayRequestQueries(string constr, ILogger<GatewayRequestQueries> logger)
        {
            _connectionString = !string.IsNullOrWhiteSpace(constr) ? constr : throw new ArgumentNullException(nameof(constr));
            _logger = logger;
            _retryPolicy = Policy.Handle<SqlException>(ex => IsTransient(ex))
                                    .Or<TimeoutException>()
                                    .WaitAndRetryAsync(
                                        retryCount: 3,
                                        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                                        onRetry: (exception, timeSpan, context) =>
                                        {
                                            // Log or handle the retry attempt
                                            _logger.LogInformation($"Retrying due to: {exception.Message}");
                                        });
        }

        public async Task<IEnumerable<RSIMessage>> GetRSIMessagesFromDbAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () => {
                using (var connection = new SqlConnection(_connectionString))
                {
                    if (connection.State == ConnectionState.Closed){
                        try{
                            await connection.OpenAsync();
                        }
                        catch (Exception ex){
                            throw new Exception(ex.Message);
                        }
                    }
                    return await connection.QueryAsync<RSIMessage>("dbo.spGetRsiMessages", commandType: CommandType.StoredProcedure);
                }
            });
        }

        public async Task<IEnumerable<Common>> SetRsiMessagesToAckedAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () => {
                using (var connection = new SqlConnection(_connectionString))
                {
                    if (connection.State == ConnectionState.Closed)
                    {
                        try
                        {
                            await connection.OpenAsync();
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(ex.Message);
                        }
                    }
                    return await connection.QueryAsync<Common>("dbo.spSetMessagesToAck", commandType: CommandType.StoredProcedure);
                }
            });
        }

        private bool IsTransient(SqlException ex)
        {
            // Check the exception code and return true if it is transient
            // List of transient error numbers can be found here:
            // https://docs.microsoft.com/en-us/azure/sql-database/sql-database-develop-error-messages
            var transientErrorNumbers = new[] { 4060, 10928, 10929, 40197, 40501, 40613 };
            return Array.Exists(transientErrorNumbers, e => e == ex.Number);
        }
    }
}
