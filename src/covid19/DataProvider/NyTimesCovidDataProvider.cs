using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using covid19.Services.Models;
using covid19.Services.Services;
using CsvHelper;
using Microsoft.Extensions.Options;
using Octokit;
using Serilog;
using Serilog.Core;

namespace covid19.Services.DataProvider
{
    public interface INyTimesCovidDataProvider
    {
        void Run(bool forcePull);

        IEnumerable<NytimesCountyCovidRow> ProcessedNytimesCountyCovidRows { get; }
    }

    public class NyTimesCovidDataProvider : INyTimesCovidDataProvider
    {
        const string UNPARSED_DATA = "NyTimesCovidRawData.txt";
        private const string PREPARED_DATA = "NyTimesPreparedCountyCovidData.csv";

        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;
        private readonly IOctoKitGitHubClient _covidGitHubClient;
        private IProcessHistory _processHistory;
        private IEnumerable<NytimesCountyCovidRow> _preparedData;
        private DateTime _covidThisRun;
        private DateTime _covidCountyDataThisLatestCheckin;
        private IProcessHistory ProcessHistory { get; }

        public NyTimesCovidDataProvider(IOptions<AppSettings> settings, ILogger logger, IProcessHistory processHistory,
            IOctoKitGitHubClient covidGitHubClient)
        {
            _processHistory = processHistory;
            _settings = settings;
            _logger = logger;
            _covidGitHubClient = covidGitHubClient;
            _covidThisRun = DateTime.Now;
            _covidCountyDataThisLatestCheckin = DateTime.Now;
        }

        public IEnumerable<NytimesCountyCovidRow> ProcessedNytimesCountyCovidRows
        {
            get { return _preparedData; }
        }

        public void Run(bool forcePull)
        {
            //TODO: 
            // 1. Check if the file has been downloaded today
            //    yes - Check github latest check-in and compare download time 
            //        - if git hub time is later, pull file process
            //        - if no use local file.
            //    no - Pull file process
            // 2. Pull file
            //    check
            
            var latestCheckinDate = _covidGitHubClient.GetLatestCheckinDateForCovidData().Result;
            
            if (_processHistory != null)
            {
                _processHistory.RetrieveHistory();
            }

            if (forcePull || latestCheckinDate > _processHistory?.DateTimeLastRun)
            {
                ProcessNewData(latestCheckinDate);
                _logger.Debug(string.Format("New data pulled."));
            }
            else
            {
                LoadProcessedData();
                _logger.Debug(string.Format("Retrieved saved data with {0} rows.", _preparedData.Count().ToString()));
            }
        }

        private void ProcessNewData(DateTime latestCheckinDate)
        {
            _preparedData = EnrichPercentChangeCases(
                ParseCountyCovidRows(PullNyTimesCountyCovidData(_settings.Value.OctoKit.NyTimesCountyCovidUri)));

            SaveProcessedData();
            _processHistory.CovidCountyLatestCheckin = latestCheckinDate;
            _processHistory.DateTimeLastRun = DateTime.Now;
            _processHistory.WriteHistory();
        }


        private string[] PullNyTimesCountyCovidData(string url)
        {
            string[] data = { };

            try
            {
                using (var cli = new WebClient())
                {
                    cli.DownloadFile(new Uri(url), UNPARSED_DATA);
                }

                data = File.ReadAllLines(UNPARSED_DATA);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

//            _logger.Debug(string.Format("{0} Mb of NyTimes raw covid data downloaded.",
//                ((decimal) Encoding.Unicode.GetByteCount(data) / 1048576).ToString("F2")));

            return data;
        }

        private void LoadProcessedData()
        {
            try
            {
                using (var reader = new StreamReader(PREPARED_DATA))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<NytimesCountyCovidRow>();
                    _preparedData = records.ToList();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                this.Run(true);
            }
            
        }

        private void SaveProcessedData()
        {
            using (var writer = new StreamWriter(PREPARED_DATA))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(_preparedData);
            }
        }

        private IEnumerable<NytimesCountyCovidRow> ParseCountyCovidRows(string[] data)
        {
            var covidData = new List<NytimesCountyCovidRow>();
            var covidErrorData = new List<string>();
            int errorRows = 0;

            foreach (var row in data)
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

        private IEnumerable<NytimesCountyCovidRow> EnrichPercentChangeCases(
            IEnumerable<NytimesCountyCovidRow> parsedData)
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