using covid19.Services.Models;
using Microsoft.Extensions.Options;
using Octokit;
using Serilog;

namespace covid19.Services.Services
{
    //var client = serviceProvider.GetService<INyTimesCovidService>().GetGitHubClient();
    //User user = await client.User.Current();
    //var NyTimesCovid = await client.Repository.Content.GetAllContents("nytimes", "covid-19-data");
    
    public class OctoKitGitHubClient : IOctoKitGitHubClient
    {
        private IGitHubClient _gitHubClient;
        private IOptions<AppSettings> _settings;
        private ILogger _logger;

        OctoKitGitHubClient(IOptions<AppSettings> settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger.ForContext<OctoKitGitHubClient>();
            _gitHubClient = GetGitHubClient();
        }

        public IGitHubClient GetGitHubClient()
        {
            var productInformation = new ProductHeaderValue(_settings.Value.ConsoleTitle);
            var credentials = new Credentials(_settings.Value.OctaKit.Username,
                _settings.Value.OctaKit.Password, AuthenticationType.Basic);

            return new GitHubClient(productInformation) {Credentials = credentials};
        }
    }

    public interface IOctoKitGitHubClient
    {
        IGitHubClient GetGitHubClient();
    }
}