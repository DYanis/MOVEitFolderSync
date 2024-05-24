using Application.Application;
using Application.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            (string username, string password) = GetCredentials();

            Microsoft.Extensions.Configuration.IConfiguration configuration = ConfigurationExtensions.BuildConfiguration();
            IHost host = ConfigurationExtensions.CreateHostBuilder(args, configuration, username, password).Build();

            ApplicationRunner runner = host.Services.GetRequiredService<ApplicationRunner>();
            await runner.RunAsync();
        }

        private static (string, string) GetCredentials()
        {
            Console.Write("Enter your MOVEit username: ");
            string? username = Console.ReadLine();

            Console.Write("Enter your MOVEit password: ");
            string? password = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Username and password cannot be empty.");
                Environment.Exit(1);
            }

            return (username, password);
        }
    }
}
