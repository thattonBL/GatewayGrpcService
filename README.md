# GatewayGrpcService
Demo of using Grpc in .NET to perform Gateway like tasks of reading submitting messages to and from an SQL database

This is a development project and requires that you have docker desktop installed. Run the docker-compose build in Debug mode in visual studio and go to the https assigned port for the GatewayGrpcService container. Add /swagger/index.html to the url and you will land oh the Grpc Swagger page and try out the service. Alternatively you can use the GatewayGrpcServiceClient project.

