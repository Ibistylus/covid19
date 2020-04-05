
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyStandardSolution.Models;
using Octokit;
using Serilog;

namespace MyStandardSolution
{
    class Program
    {
        static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, args);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var asp = serviceProvider.GetService<IOptions<AppSettings>>();

            var productInformation = new ProductHeaderValue("IbisStylus");
            var credentials = new Credentials("user","Password", AuthenticationType.Basic);
            var client = new GitHubClient(productInformation);
            
        }

        private static void ConfigureServices(IServiceCollection serviceCollection, string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
//                .AddEnvironmentVariables("COVID_16")
//                .AddCommandLine(args)
                .Build();
            
            var log = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();

            serviceCollection.AddSingleton(log);

            serviceCollection.AddOptions();
            serviceCollection.Configure<AppSettings>(config.GetSection("Configuration"));
            serviceCollection.AddSingleton(config);
            ConfigureConsole(config);
            

        }

        private static void ConfigureConsole(IConfigurationRoot configuration)
        {
            System.Console.Title = configuration.GetSection("Configuration:ConsoleTitle").Value;
        }
    }
}