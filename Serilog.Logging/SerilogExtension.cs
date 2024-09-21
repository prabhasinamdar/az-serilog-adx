using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Sinks.AzureDataExplorer;
using Serilog.Sinks.AzureDataExplorer.Extensions;


namespace Serilog.Logging
{
    public static class SerilogExtension
    {
        private static bool enableAppInsight = false;
        public static IHostBuilder UseCustomSerilog(this IHostBuilder hostBuilder)
        {
            var builder = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json", optional: false)
             .AddEnvironmentVariables();             

            IConfiguration config = builder.Build();

            var aiOptions = new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions();
            var enableAdaptiveSampling = config.GetValue<bool>("ApplicationInsights:EnableAdaptiveSampling");

            //Disables adaptive sampling.
            aiOptions.EnableAdaptiveSampling = enableAdaptiveSampling;
            aiOptions.EnableQuickPulseMetricStream = false;

            hostBuilder.ConfigureServices(services => services.AddApplicationInsightsTelemetry(aiOptions));
            //Middleware
            hostBuilder.ConfigureServices(services => services.AddTransient<RequestResponseMiddleware>());



            string appInsightConnStr = config.GetValue<string>("ApplicationInsights:ConnectionString") ?? "";

            var ingestionUri = config.GetValue<string>("Adx:IngestionUri");
            var databaseName = config.GetValue<string>("Adx:DatabaseName");
            var tableName = config.GetValue<string>("Adx:TableName");
            var flushImmediately = config.GetValue<bool>("Adx:FlushImmediately");
            var bufferBaseFileName = config.GetValue<string>("Adx:BufferBaseFileName");
            var applicationClientID = config.GetValue<string>("Adx:ApplicationClientId");
            var applicationKey = config.GetValue<string>("Adx:ApplicationKey");
            var TenantID = config.GetValue<string>("Adx:TenantID");

            if (appInsightConnStr != "__AIConnectionString__")
                enableAppInsight = true;

            if (enableAppInsight)
            {
                var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
                telemetryConfiguration.ConnectionString = appInsightConnStr;
                hostBuilder.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.AzureDataExplorerSink(new AzureDataExplorerSinkOptions
                {
                    IngestionEndpointUri = ingestionUri,
                    DatabaseName = databaseName,
                    TableName = tableName,
                    FlushImmediately = flushImmediately,
                    BufferBaseFileName = bufferBaseFileName,
                    BatchPostingLimit = 10,
                    Period = TimeSpan.FromSeconds(5),

                    ColumnsMapping = new[]
                        {
                            new SinkColumnMapping { ColumnName ="Timestamp", ColumnType ="datetime", ValuePath = "$.Timestamp" } ,
                            new SinkColumnMapping { ColumnName ="Level", ColumnType ="string", ValuePath = "$.Level" } ,
                            new SinkColumnMapping { ColumnName ="Message", ColumnType ="string", ValuePath = "$.Message" } ,
                            new SinkColumnMapping { ColumnName ="Exception", ColumnType ="string", ValuePath = "$.ExceptionEx" } ,
                            new SinkColumnMapping { ColumnName ="Properties", ColumnType ="dynamic", ValuePath = "$.Properties" } ,
                            new SinkColumnMapping { ColumnName ="CorrelationId", ColumnType ="guid", ValuePath = "$.Properties.CorrelationId" } ,
                            new SinkColumnMapping { ColumnName ="Elapsed", ColumnType ="int", ValuePath = "$.Properties.Elapsed" }                            
                        }
                }.WithAadApplicationKey(applicationClientID, applicationKey, TenantID))
                    .WriteTo.ApplicationInsights(telemetryConfiguration, new CustomLogConverter())
                );
            }
            return hostBuilder;
        }
        public static IApplicationBuilder UseCustomSerilogRequestLogging(this IApplicationBuilder app)
        {
            if (enableAppInsight)
            {
                app.UseMiddleware<RequestResponseMiddleware>();
                app.UseSerilogRequestLogging();
            }
            return app;
        }
    }
}
