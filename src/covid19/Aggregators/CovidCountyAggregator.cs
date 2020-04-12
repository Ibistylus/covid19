namespace covid19.Services
{
    public static class CovidCountyAggregator
    {
        public static decimal? PercentChange(decimal previouValue, decimal currentValue)
        {
            return (currentValue - previouValue) / previouValue * (decimal) 100.00;
        }
    }
}