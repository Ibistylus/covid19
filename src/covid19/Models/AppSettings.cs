namespace covid19.Services.Models
{
    public class AppSettings
    {
        public string ConsoleTitle { get; set; }
        public OctokitConfig OctoKit { get; set; }


        public class OctokitConfig
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string RepoOwner { get; set; }
            public string RepoName { get; set; }
            public string RepoBranch { get; set; }
            public string NyTimesCountyCovidUri { get; set; }
            public string NyTimesCountyCovidPath { get; set; }
        }
    }
}