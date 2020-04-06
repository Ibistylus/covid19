using System.Collections.Generic;
using covid19.Services.Models;
using Octokit;

namespace covid19.Services
{
    public static class CovidCountyAggregator
    {
        public static decimal? PercenChange(decimal previouValue, decimal currentValue)
        {
            return  (decimal?)( (currentValue - previouValue) /  (previouValue) * (decimal)100.00);
        }
    }
}