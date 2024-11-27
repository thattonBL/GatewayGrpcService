using EventBus.Abstractions;
using GatewayGrpcService.IntegrationEvents.EventHandling;
using GatewayGrpcService.IntegrationEvents.Events;
using GatewayGrpcService.Queries;
using GatewayGrpcService.Services;
using Services.Common;
using GatewayGrpcService.Data;
using Microsoft.EntityFrameworkCore;
using GatewayGrpcService.Data.Repostories;
using Events.Common.Events;
using GatewayGrpcService.Infrastructure;
using GatewayGrpcService.Protos;
using Serilog;
using GatewayGrpcService.Factories;
using Elastic.CommonSchema.Serilog;
using Elastic.Serilog.Sinks;
using Elastic.Ingest.Elasticsearch;

namespace GatewayGrpcService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //Adds the event Bus / RabbitMQ
            builder.AddServiceDefaults();

            // Add services to the container.
            //Adds the Event Bus required for integration events
            builder.AddServiceDefaults();

            var connectionString = Environment.GetEnvironmentVariable("SQL_DB_CONNECTION_STRING");
            if (String.IsNullOrEmpty(connectionString))
            {
                connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            }

            builder.Services.AddScoped<IGatewayRequestQueries>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<GatewayRequestQueries>>();
                return new GatewayRequestQueries(connectionString, logger);
            });

            builder.Services.AddDbContext<GatewayGrpcContext>(options => options.UseSqlServer(connectionString));
            //builder.Services.AddDbContext<GatewayGrpcContext>(options =>
            //{
            //    options.UseSqlServer(connectionString,
            //        sqlServerOptionsAction: sqlOptions =>
            //        {
            //            sqlOptions.EnableRetryOnFailure(
            //                            maxRetryCount: 5,
            //                            maxRetryDelay: TimeSpan.FromSeconds(30),
            //                            errorNumbersToAdd: null);
            //        });
            //});

            //builder.Host.UseSerilog((context, configuration) =>
            //{
            //    var httpAccessor = context.Configuration.Get<HttpContextAccessor>();
            //    configuration.ReadFrom.Configuration(context.Configuration)
            //                 .Enrich.WithEcsHttpContext(httpAccessor)
            //                 .Enrich.WithEnvironmentName()
            //                 .WriteTo.ElasticCloud(context.Configuration["ElasticCloud:CloudId"], context.Configuration["ElasticCloud:CloudUser"], context.Configuration["ElasticCloud:CloudPass"], opts =>
            //                 {
            //                     opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("gateway-grpc-service-new-logs");
            //                     opts.BootstrapMethod = BootstrapMethod.Failure;
            //                 });
            //});

            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            });

            var services = builder.Services;

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(Program));

                //cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
                //cfg.AddOpenBehavior(typeof(ValidatorBehavior<,>));
                //cfg.AddOpenBehavior(typeof(TransactionBehaviour<,>));
            });



            builder.Services.AddTransient<GrpcExceptionInterceptor>();

            var building33MockApiAddress = Environment.GetEnvironmentVariable("CLIENT_BASE_URL");
            if (String.IsNullOrEmpty(building33MockApiAddress))
            {
                building33MockApiAddress = builder.Configuration["MessageServices:Building33MockApiUri"];
            }

            if (String.IsNullOrEmpty(building33MockApiAddress))
            {
                throw new Exception("building33MockApiAddress not set");
            }

            builder.Services.AddGrpcClient<GatewayGrpcMessagingService.GatewayGrpcMessagingServiceClient>((services, options) =>
            {
                options.Address = new Uri(building33MockApiAddress);
            }).AddInterceptor<GrpcExceptionInterceptor>();

            builder.Services.AddHttpClient<HttpMessageDispatchService>((services, client) =>
            {
               client.BaseAddress = new Uri(building33MockApiAddress);
            });
            
            builder.Services.AddGrpc().AddJsonTranscoding();
            builder.Services.AddGrpcReflection();
            builder.Services.AddGrpcSwagger();

            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Gateway Grpc Service", Version = "v1" });
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });

            builder.Services.AddTransient<NewRsiMessageSubmittedIntegrationEventHandler>();
            builder.Services.AddTransient<StopConsumerRequestIntegrationEventHandler>();
            builder.Services.AddTransient<RestartConsumerRequestIntegrationEventHandler>();

            builder.Services.AddTransient<ISQLMessageServices, SQLMessageServices>();
            
            builder.Services.AddScoped<IGatewayGrpcMessageRepo, GatewayGrpcMessageRepo>();

            builder.Services.AddSingleton<IMessageServiceControl, MessageServiceControl>();

            builder.Services.AddScoped<GrpcMessageDispatchService>();
            builder.Services.AddScoped<HttpMessageDispatchService>();
            builder.Services.AddScoped<IMessageDispatchServiceFacrory, MessageDispatchServiceFacrory>();

            var app = builder.Build();

            app.Use((context, next) =>
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                return next.Invoke();
            });
            app.UseSwagger();

            var contentRoot = builder.Configuration.GetValue<string>(WebHostDefaults.ContentRootKey);

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "gRPC using .NET7 Demo");
                //c.ConfigObject.Urls = new[] { new UrlDescriptor { Name = "gRPC", Url = Path.Combine(contentRoot, "swagger/custom-swagger-config.json") }};
            });

            app.UseCors("AllowAll");
            // Configure the HTTP request pipeline.
            //app.MapGrpcService<GrpcMessageService>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            app.MapSwagger();
            app.MapGrpcReflectionService();

            app.UseSerilogRequestLogging();

            var eventBus = app.Services.GetRequiredService<IEventBus>();
            eventBus.Subscribe<NewRsiMessageSubmittedIntegrationEvent, NewRsiMessageSubmittedIntegrationEventHandler>(NewRsiMessageSubmittedIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<StopConsumerRequestIntegrationEvent, StopConsumerRequestIntegrationEventHandler>(StopConsumerRequestIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<RestartConsumerRequestIntegrationEvent, RestartConsumerRequestIntegrationEventHandler>(RestartConsumerRequestIntegrationEvent.EVENT_NAME);

            app.Run();
        }
    }
}