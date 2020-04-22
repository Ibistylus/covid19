using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using covid19.Services.DataProvider;
using covid19.Services.Models;
using covid19.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace covid19.Services
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, args);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var nyTimesCovidService = serviceProvider.GetService<INyTimesCovidService>();
            var nyTimesCovidDataProvider = serviceProvider.GetService<INyTimesCovidDataProvider>();
            nyTimesCovidDataProvider.Run(false);
            var resultsDekalb = nyTimesCovidService.GetNyTimesCountyCovidDataByCounty("georgia", "dekalb");
            //            var resultsCobb = nyTimesCovidService.GetNyTimesCountyCovidDataByCounty("georgia", "cobb");

            foreach (var covidRow in resultsDekalb)
            {
                StringBuilder sbResults = new StringBuilder();

                sbResults.Append(covidRow.Date.ToString("yyyy-MM-dd"));
                sbResults.Append(" ");
                sbResults.Append(covidRow.CasesPercentChange?.ToString( "#.##" ) + "%");
                
                Console.WriteLine(sbResults);
            }
            return 1;
        }

        private static void ConfigureServices(IServiceCollection serviceCollection, string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile("appsettings.development.json", true, true)
                .AddEnvironmentVariables("COVID_16")
                .AddCommandLine(args)
                .Build();

            Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();

            serviceCollection.AddSingleton(typeof(ILogger), Log.Logger);

            serviceCollection
                .AddOptions()
                .Configure<AppSettings>(config.GetSection("Configuration"))
                .AddSingleton(config)
                .AddSingleton<IOctoKitGitHubClient, OctoKitGitHubClient>()
                .AddSingleton<INyTimesCovidDataProvider, NyTimesCovidDataProvider>()
                .AddSingleton<IProcessHistory, ProcessHistory>()
                .AddSingleton<INyTimesCovidService, NyTimesCovidService>();

            ConfigureConsole(config);
        }

        private static void ConfigureConsole(IConfigurationRoot configuration)
        {
            Console.Title = configuration.GetSection("Configuration:ConsoleTitle").Value;
        }
    }
}