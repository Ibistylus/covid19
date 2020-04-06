using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using covid19.Services.Models;
using covid19.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;
using System.Net.Http;
using Serilog;
using Serilog.Core;

namespace covid19.Services
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, args);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var configurationOptions = serviceProvider.GetService<IOptions<AppSettings>>();

            var NyTimesService = serviceProvider.GetService<INyTimesCovidService>();
            var resultsDekalb = serviceProvider.GetService<INyTimesCovidService>()
                .GetNyTimesCountyCovidDataByCounty("georgia", "dekalb");
            var resultsCobb = serviceProvider.GetService<INyTimesCovidService>()
                            .GetNyTimesCountyCovidDataByCounty("georgia", "cobb");
                        
            return 1;
        }

        private static void ConfigureServices(IServiceCollection serviceCollection, string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("COVID_16")
                .AddCommandLine(args)
                .Build();

            Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();

            serviceCollection.AddSingleton(typeof(ILogger), Log.Logger);

            serviceCollection.AddOptions()
                .Configure<AppSettings>(config.GetSection("Configuration"))
                .AddSingleton(config)
                .AddSingleton<Services.INyTimesCovidService, Services.NyTimesCovidService>()
                .AddSingleton<IOctoKitGitHubClient, OctoKitGitHubClient>();
            
            ConfigureConsole(config);
        }

        private static void ConfigureConsole(IConfigurationRoot configuration)
        {
            System.Console.Title = configuration.GetSection("Configuration:ConsoleTitle").Value;
        }
    }
}