using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using covid19.Services.DataProvider;
using covid19.Services.Models;
using Microsoft.Extensions.Options;
using Octokit;
using Serilog;
using Serilog.Core;

namespace covid19.Services.Services
{
    interface INyTimesCovidService
    {
        List<NytimesCountyCovidRow> NyTimesCountyCovidData { get; }
        List<NytimesCountyCovidRow> GetNyTimesCountyCovidDataByCounty(string state, string county);
    }

    public class NyTimesCovidService : INyTimesCovidService
    {
        private readonly IOptions<AppSettings> _settings;
        private readonly INyTimesCovidDataProvider _covidProvider;
        private IGitHubClient _gitHubClient;
        private List<NytimesCountyCovidRow> _nycoviddata;
        private string _nycoviddataraw;
        private ILogger _logger;

        public NyTimesCovidService(IOptions<AppSettings> settings, ILogger logger, INyTimesCovidDataProvider covidProvider)
        {
            _settings = settings;
            _covidProvider = covidProvider;
            _logger = logger.ForContext<NyTimesCovidService>();
            covidProvider.Run(false);
            _nycoviddata = covidProvider.ProcessedNytimesCountyCovidRows.ToList();
        }

        public List<NytimesCountyCovidRow> NyTimesCountyCovidData
        {
            get { return _nycoviddata; }
        }

        public List<NytimesCountyCovidRow> GetNyTimesCountyCovidDataByCounty(string state, string county)
        {
            return NyTimesCountyCovidData
                .Where(x => x.State.ToLower() == state.ToLower() && x.County.ToLower() == county.ToLower()).ToList();
        }

    }
}