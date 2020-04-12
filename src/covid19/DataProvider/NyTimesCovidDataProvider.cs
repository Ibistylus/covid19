using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using covid19.Services.Models;
using covid19.Services.Services;
using CsvHelper;
using Microsoft.Extensions.Options;
using Serilog;

namespace covid19.Services.DataProvider
{
    public interface INyTimesCovidDataProvider
    {
        IEnumerable<NytimesCountyCovidRow> ProcessedNytimesCountyCovidRows { get; }
        void Run(bool forcePull);
    }

    public class NyTimesCovidDataProvider : INyTimesCovidDataProvider
    {
        private const string UNPARSED_DATA = "NyTimesCovidRawData.txt";
        private const string PREPARED_DATA = "NyTimesPreparedCountyCovidData.csv";
        private readonly IOctoKitGitHubClient _covidGitHubClient;
        private readonly ILogger _logger;

        private readonly IOptions<AppSettings> _settings;
        private DateTime _covidCountyDataThisLatestCheckin;
        private DateTime _covidThisRun;
        private readonly IProcessHistory _processHistory;

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

        private IProcessHistory ProcessHistory { get; }

        public IEnumerable<NytimesCountyCovidRow> ProcessedNytimesCountyCovidRows { get; private set; }

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

            if (_processHistory != null) _processHistory.RetrieveHistory();

            if (forcePull || latestCheckinDate > _processHistory?.DateTimeLastRun)
            {
                ProcessNewData(latestCheckinDate);
                _logger.Debug("New data pulled.");
            }
            else
            {
                LoadProcessedData();
                _logger.Debug(string.Format("Retrieved saved data with {0} rows.",
                    ProcessedNytimesCountyCovidRows.Count().ToString()));
            }
        }

        private void ProcessNewData(DateTime latestCheckinDate)
        {
            ProcessedNytimesCountyCovidRows = EnrichPercentChangeCases(
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
                    ProcessedNytimesCountyCovidRows = records.ToList();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Run(true);
            }
        }

        private void SaveProcessedData()
        {
            using (var writer = new StreamWriter(PREPARED_DATA))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(ProcessedNytimesCountyCovidRows);
            }
        }

        private IEnumerable<NytimesCountyCovidRow> ParseCountyCovidRows(string[] data)
        {
            var covidData = new List<NytimesCountyCovidRow>();
            var covidErrorData = new List<string>();
            var errorRows = 0;

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
            var prevCounty = string.Empty;
            var prevState = string.Empty;

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

                    covidRow.CasesPercentChange = decimal.Zero;
                    covidRow.DeathPercentChange = decimal.Zero;

                    continue;
                }

                try
                {
                    if (prevDeaths > 0)
                        covidRow.DeathPercentChange =
                            CovidCountyAggregator.PercentChange((decimal) prevDeaths, (decimal) covidRow.Deaths);
                    else
                        covidRow.DeathPercentChange = decimal.Zero;

                    if (prevCases > 0)
                        covidRow.CasesPercentChange =
                            CovidCountyAggregator.PercentChange((decimal) prevCases, (decimal) covidRow.Cases);
                    else
                        covidRow.CasesPercentChange = decimal.Zero;

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