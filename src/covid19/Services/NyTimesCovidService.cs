using System.Collections.Generic;
using System.Linq;
using covid19.Services.DataProvider;
using covid19.Services.Models;
using Microsoft.Extensions.Options;
using Octokit;
using Serilog;

namespace covid19.Services.Services
{
    internal interface INyTimesCovidService
    {
        List<NytimesCountyCovidRow> NyTimesCountyCovidData { get; }
        List<NytimesCountyCovidRow> GetNyTimesCountyCovidDataByCounty(string state, string county);
    }

    public class NyTimesCovidService : INyTimesCovidService
    {
        private readonly INyTimesCovidDataProvider _covidProvider;
        private readonly IOptions<AppSettings> _settings;
        private IGitHubClient _gitHubClient;
        private ILogger _logger;
        private string _nycoviddataraw;

        public NyTimesCovidService(IOptions<AppSettings> settings, ILogger logger,
            INyTimesCovidDataProvider covidProvider)
        {
            _settings = settings;
            _covidProvider = covidProvider;
            _logger = logger.ForContext<NyTimesCovidService>();
            covidProvider.Run(false);
            NyTimesCountyCovidData = covidProvider.ProcessedNytimesCountyCovidRows.ToList();
        }

        public List<NytimesCountyCovidRow> NyTimesCountyCovidData { get; }

        public List<NytimesCountyCovidRow> GetNyTimesCountyCovidDataByCounty(string state, string county)
        {
            return NyTimesCountyCovidData
                .Where(x => x.State.ToLower() == state.ToLower() && x.County.ToLower() == county.ToLower()).ToList();
        }
    }
}