using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
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
        private IGitHubClient _gitHubClient;
        private List<NytimesCountyCovidRow> _nycoviddata;
        private string _nycoviddataraw;
        private ILogger _logger;

        public NyTimesCovidService(IOptions<AppSettings> settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger.ForContext<NyTimesCovidService>();
            _nycoviddata = GetNyTimesCovidData();

            _logger.Debug("Construction complete.");
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

        /// <summary>
        /// Gets and parses NyTimes Covid data.
        /// </summary>
        /// <returns>A parsed list of NytimesCountyCovidRow</returns>
        private List<NytimesCountyCovidRow> GetNyTimesCovidData()
        {
            var parsedData = ParseCountyCovidRows(GetNyTimesCovidRawData(_settings.Value.NyTimesCountyCovidUri));
            var enrichedData = EnrichPercentChangeCases(parsedData);
            return enrichedData.ToList();
        }

        private List<NytimesCountyCovidRow> ParseCountyCovidRows(string data)
        {
            var rawRows = data.Split("\n");

            var covidData = new List<NytimesCountyCovidRow>();
            var covidErrorData = new List<string>();
            int errorRows = 0;

            foreach (var row in rawRows)
            {
                var cr = new NytimesCountyCovidRow();
                var rSplit = row.Split(",");
                try
                {
                    cr.Date = DateTime.Parse(rSplit[0]);
                    cr.County = rSplit[1];
                    cr.State = rSplit[2] ?? "";
                    cr.Fips = rSplit[3];
                    cr.Cases = int.Parse(rSplit[4]);
                    cr.Deaths = int.Parse(rSplit[5]);

                    covidData.Add(cr);
                }
                catch (Exception e)
                {
                    covidErrorData.Add(row);
                    errorRows += 1;
                }
            }

            _logger.Debug(string.Format("{0} rows downloaded from NyTimes. {1} error rows captured.",
                covidData.Count.ToString(), covidErrorData.Count.ToString()));

            return covidData;
        }

        private string GetNyTimesCovidRawData(string uri)
        {
            var cli = new WebClient();

            string data = string.Empty;

            try
            {
                data = cli.DownloadString(uri);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            _logger.Debug(string.Format("{0} Mb of NyTimes raw covid data downloaded.",
                ((decimal) Encoding.Unicode.GetByteCount(data) / 1048576).ToString("F2")));

            return data;
        }

        private IEnumerable<NytimesCountyCovidRow> EnrichPercentChangeCases(List<NytimesCountyCovidRow> parsedData)
        {
            decimal percentChange = 0;
            int? prevCases = 0;
            int? prevDeaths = 0;
            string prevCounty = string.Empty;
            string prevState = string.Empty;

            var enrichedData = parsedData
                .OrderBy(o => o.State)
                .ThenBy(o => o.County)
                .ThenBy(o => o.Date)
                .Select(a => a);

            foreach (var covidRow in enrichedData)
            {
                //Check to see that we're on the same county
                //Check to see that we're on the same state

                if (prevCounty != covidRow.County || prevState != covidRow.State)
                {
                    prevState = covidRow.State;
                    prevCounty = covidRow.County;

                    prevCases = covidRow.Cases;
                    prevDeaths = covidRow.Deaths;

                    covidRow.CasesPercentChange = Decimal.Zero;
                    covidRow.DeathPercentChange = Decimal.Zero;

                    continue;
                }

                try
                {
                    if (prevDeaths > 0)
                    {
                        covidRow.DeathPercentChange =
                            CovidCountyAggregator.PercenChange((decimal) prevDeaths, (decimal) covidRow.Deaths);
                    }
                    else
                    {
                        covidRow.DeathPercentChange = Decimal.Zero;
                    }

                    if (prevCases > 0)
                    {
                        covidRow.CasesPercentChange =
                            CovidCountyAggregator.PercenChange((decimal) prevCases, (decimal) covidRow.Cases);
                    }
                    else
                    {
                        covidRow.CasesPercentChange = Decimal.Zero;
                    }

                    prevDeaths = covidRow.Deaths;
                    prevCases = covidRow.Cases;
                    prevCounty = covidRow.County;
                    prevState = covidRow.State;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return enrichedData;
        }
    }
}