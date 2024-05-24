using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Application.Extensions
{
    public static class ConfigurationExtensions
    {
        public static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configuration, string username, string password) =>
           Host.CreateDefaultBuilder(args)
               .ConfigureServices((context, services) =>
               {
                   services.ConfigureServices(configuration, username, password);
               })
               .UseSerilog();
    }
}
