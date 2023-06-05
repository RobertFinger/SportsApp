
using SportData.Requests;


namespace SportData.Models
{
    public class DataAgeMonitorParams : IHttpRequest
    {
        public int id { get; set; }
        public DateTime LastRefreshed { get; set; }
        public int AveragePlayerAge { get; set; }
        public string Sport { get; set; }
    }
}
