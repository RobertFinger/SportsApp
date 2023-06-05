
using Newtonsoft.Json;
using SportData.Models;

namespace SportsData.Models
{
    public class DbObjectPlayer
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }


        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }
        public Sport Sport { get; set; }
        public string NameBrief { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
        public int Age { get; set; }
        public int AveragePositionAgeDiff { get; set; }
        public DateTime LastImported { get; set; } = DateTime.UtcNow;
    }
    public class ResponseObject
    {
        public string uri { get; set; }
        public int statusCode { get; set; }
        public string uriAlias { get; set; }
        public string statusMessage { get; set; }
        public BodyObject body { get; set; }
    }

    public class BodyObject
    {
        public List<DbObjectPlayer> players { get; set; }
    }
}
