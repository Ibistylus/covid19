using System;
using System.IO;
using System.Text.Json;

namespace covid19.Services.Models
{
    public interface IProcessHistory
    {
        DateTime DateTimeLastRun { get; set; }
        DateTime CovidCountyLatestCheckin { get; set; }

        bool WriteHistory();
        bool RetrieveHistory();
    }

    public class ProcessHistory : IProcessHistory
    {
        private readonly string FILE_HISTORY_NAME = "fileHistory.json";

        public ProcessHistory()
        {
            DateTimeLastRun = DateTime.Now - new TimeSpan(1, 0, 0, 0);
            CovidCountyLatestCheckin = DateTime.Now - new TimeSpan(1, 0, 0, 0);
        }

        public DateTime DateTimeLastRun { get; set; }
        public DateTime CovidCountyLatestCheckin { get; set; }

        public bool WriteHistory()
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(this);
                File.WriteAllText(FILE_HISTORY_NAME, jsonString);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return false;
        }

        public bool RetrieveHistory()
        {
            if (File.Exists(FILE_HISTORY_NAME))
            {
                var history = File.ReadAllText(FILE_HISTORY_NAME);
                var LastRunProcessHistory = JsonSerializer.Deserialize<ProcessHistory>(history);
                DateTimeLastRun = LastRunProcessHistory.DateTimeLastRun;
                CovidCountyLatestCheckin = LastRunProcessHistory.CovidCountyLatestCheckin;
                return true;
            }

            return false;
        }
    }
}