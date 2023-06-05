using Newtonsoft.Json;

namespace SportsApp.Models
{
    public class Player
    {
        [JsonProperty(PropertyName = "Id")]
        public string id { get; set; }

        [JsonProperty(PropertyName = "NameBrief")]
        public string name_brief { get; set; }
        [JsonProperty(PropertyName = "FirstName")]
        public string first_name { get; set; }
        [JsonProperty(PropertyName = "LastName")]
        public string last_name { get; set; }
        [JsonProperty(PropertyName = "Position")]
        public string position { get; set; }
        [JsonProperty(PropertyName = "Age")]
        public int age { get; set; }
        [JsonProperty(PropertyName = "AveragePositionAgeDiff")]
        public int average_position_age_diff { get; set; }
    }

    public class ResponseObject
    {
        [JsonProperty("message")]
        public List<Player> Players { get; set; }
    }
}
