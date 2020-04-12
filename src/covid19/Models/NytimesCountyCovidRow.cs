using System;

namespace covid19.Services.Models
{
    public class NytimesCountyCovidRow
    {
        public DateTime Date { get; set; }

        public string County { get; set; }
        public string State { get; set; }
        public string Fips { get; set; }
        public int? Cases { get; set; }
        public int? Deaths { get; set; }

        public decimal? CasesPercentChange { get; set; }
        public decimal? DeathPercentChange { get; set; }
    }
}