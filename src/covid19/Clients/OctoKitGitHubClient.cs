using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using covid19.Services.Models;
using Microsoft.Extensions.Options;
using Octokit;
using Serilog;

namespace covid19.Services.Services
{
    //var client = serviceProvider.GetService<INyTimesCovidService>().GetGitHubClient();
    //User user = await client.User.Current();
    //var NyTimesCovid = await client.Repository.Content.GetAllContents("nytimes", "covid-19-data");

    public interface IOctoKitGitHubClient
    {
        IGitHubClient GetGitHubClient();
        IGitHubClient GetGitHubClient(string userName, string password);
        Task<IReadOnlyList<RepositoryContent>> GetRepo(string gitHubUser, string repo);
        Task<DateTime> GetLatestCheckinDateForCovidData();
    }

    public class OctoKitGitHubClient : IOctoKitGitHubClient
    {
        private readonly IGitHubClient _gitHubClient;
        private ILogger _logger;
        private readonly IOptions<AppSettings> _settings;

        public OctoKitGitHubClient(IOptions<AppSettings> settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger.ForContext<OctoKitGitHubClient>();
            _gitHubClient = GetGitHubClient();
        }

        public IGitHubClient GetGitHubClient()
        {
            return GetGitHubClient(_settings.Value.OctoKit.Username, _settings.Value.OctoKit.Password);
        }

        public IGitHubClient GetGitHubClient(string userName, string password)
        {
            var productInformation = new ProductHeaderValue(_settings.Value.ConsoleTitle);
            var credentials = new Credentials(userName,
                password, AuthenticationType.Basic);

            return new GitHubClient(productInformation) {Credentials = credentials};
        }

        public async Task<IReadOnlyList<RepositoryContent>> GetRepo(string gitHubUser, string repo)
        {
            return await _gitHubClient.Repository.Content.GetAllContents(gitHubUser, repo);
        }

        public async Task<DateTime> GetLatestCheckinDateForCovidData()
        {
            var repo = _gitHubClient.Repository.Get(_settings.Value.OctoKit.RepoOwner,
                _settings.Value.OctoKit.RepoName).Result;

            var request = new CommitRequest
                {Path = _settings.Value.OctoKit.NyTimesCountyCovidPath, Sha = _settings.Value.OctoKit.RepoBranch};

            var commitsForFile = await _gitHubClient.Repository.Commit.GetAll(repo.Id, request);
            var mostRecentCommit = commitsForFile[0];
            var authorDate = mostRecentCommit.Commit.Author.Date;
            var fileEditDate = authorDate.LocalDateTime;

            return fileEditDate;
        }
    }
}