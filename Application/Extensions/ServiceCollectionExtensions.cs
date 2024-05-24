using Application.Application;
using Core;
using Core.Configurations;
using Infrastructure;
using Infrastructure.ApiClients;
using Infrastructure.ApiClients.MoveIt;
using Infrastructure.Configurations;
using Infrastructure.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigureServices(this IServiceCollection services, IConfiguration configuration, string username, string password)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            services.AddOptions();
            services.Configure<FileSyncWatcherOptions>(configuration.GetSection("FileSyncWatcherOptions"));
            services.Configure<CloudFileSyncManagerOptions>(configuration.GetSection("CloudFileSyncManagerOptions"));
            services.Configure<ApiOptions>(configuration.GetSection("ApiOptions"));
            services.Configure<CredentialsOptions>(options =>
            {
                options.Username = username;
                options.Password = password;
            });

            services.AddSingleton(configuration);

            services.AddTransient<AuthenticationHandler>();

            services.AddHttpClient("MoveItApiClient")
                .AddHttpMessageHandler<AuthenticationHandler>();

            services.AddTransient(serviceProvider =>
            {
                IHttpClientFactory factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                HttpClient client = factory.CreateClient("MoveItApiClient");
                IOptions<ApiOptions> options = serviceProvider.GetRequiredService<IOptions<ApiOptions>>();

                MoveItApiClient moveItApiClient = new(client)
                {
                    BaseUrl = options.Value.BaseUrl
                };

                return moveItApiClient;
            });

            services.AddTransient(serviceProvider =>
            {
                IOptions<CredentialsOptions> credentialsOptions = serviceProvider.GetRequiredService<IOptions<CredentialsOptions>>();
                IOptions<ApiOptions> apiOptions = serviceProvider.GetRequiredService<IOptions<ApiOptions>>();
                ILogger<MoveItTokenProvider> logger = serviceProvider.GetRequiredService<ILogger<MoveItTokenProvider>>();

                MoveItApiClient moveItApiClient = new(new HttpClient())
                {
                    BaseUrl = apiOptions.Value.BaseUrl
                };

                return new MoveItTokenProvider(moveItApiClient, credentialsOptions, logger);
            });

            services.AddTransient<FileSyncWatcher>();
            services.AddSingleton<ICloudFileSyncManager, CloudFileSyncManager>();
            services.AddTransient<ApplicationRunner>();
        }
    }
}
