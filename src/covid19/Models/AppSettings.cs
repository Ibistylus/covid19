namespace covid19.Services.Models
{
    public class AppSettings
    {
        public string ConsoleTitle { get; set; }
        public OctakitConfig OctaKit { get; set; }

        public string NyTimesCountyCovidUri { get; set; }
        
        public class OctakitConfig
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }


}