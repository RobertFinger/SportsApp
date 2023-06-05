using MediatR;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Azure.Cosmos.Spatial;
using Newtonsoft.Json;
using SportData.Models;
using SportData.Requests;
using SportsData.Models;
using System.Text;

namespace SportData.Handlers
{
    public class SearchHandler : IRequestHandler<SearchQuery, IResult>
    {
        private readonly string _endpointUri;
        private readonly string _primaryKey;
        private CosmosClient _cosmosClient;
        private Database _database;
        private Container _container;
        private string _databaseId = "PlayerData";
        private string _containerId = "Players";
        private AverageAge _averageAge;
        public SearchHandler(IConfiguration configuration)
        {
            _endpointUri = configuration["EndPointUri"];
            _primaryKey = configuration["PrimaryKey"];

        }
        public async Task<IResult> Handle(SearchQuery request, CancellationToken cancellationToken)
        {
            try
            {
                await SetupDataStore();

                if ( await DataUpdateNeeded())
                {
                    return Results.Ok(
                        new
                        {
                            message = $"We are updating the data. Please try again in a few seconds."
                        });
                }

                var players = await SearchPlayersAsync(request);

                return Results.Ok(new
                {
                    message = players
                });

            }
            catch (Exception ex)
            {
                //Yep, I'mmmm a teapot.
                return Results.StatusCode(418);
            }

        }

        public async Task<List<DbObjectPlayer>> PopulatePlayerList(Sport sport)
        {
            await SetupDataStore();
            return await AddItemsToContainerAsync(sport);
        }

        public async Task<bool> DataUpdateNeeded()
        {
            // if we can't get their average age then the data probably aged out and it's time to repopulate the data. 
            // I'm kicking off the process to refresh the data, but I'm not waiting for it to finish. Let's tell the user to try again in a few seconds.
            // In my dev testing, refreshing a whole sport dataset can time out before the 100 second limit. That's too long for a user to wait.  

            var averageAge = await CalculateAverageAgesBySportAsync();
            if (averageAge.Baseball < 1 || averageAge.Basketball < 1 || averageAge.Football < 1)
            {              

                if (averageAge.Baseball < 1)
                {
                    PopulatePlayerList(Sport.baseball);
                }

                if (averageAge.Basketball < 1)
                {
                    PopulatePlayerList(Sport.basketball);
                }

                if (averageAge.Football < 1)
                {
                    PopulatePlayerList(Sport.football);
                }

                return true;
            }
            return false;
        }

        public async Task SetupDataStore()
        {
            this._cosmosClient = new CosmosClient(_endpointUri, _primaryKey, new CosmosClientOptions() { ApplicationName = "SportsApi" });
            this._database = await this._cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
            ContainerProperties containerProperties = new ContainerProperties(_containerId, "/partitionKey");

            //data will age out after 1 day.  This is a good default for sports data since we don't know when players ages will change.
            // this will cause a delay on the first user per day per sport.  Ideally, that would be us running a scheduled procedure to refresh the data.

            int ttlInSeconds = (int)TimeSpan.FromDays(1).TotalSeconds;
            containerProperties.DefaultTimeToLive = ttlInSeconds;
            this._container = await this._database.CreateContainerIfNotExistsAsync(containerProperties);
        }

        private async Task<List<DbObjectPlayer>> AddItemsToContainerAsync(Sport sport)
        {
            var players = await RefreshPlayerList(sport);
            foreach (var player in players)
            {
                var key = new PartitionKey(player.PartitionKey);
                try
                {
                    ItemResponse<DbObjectPlayer> playerDataResponse = await _container.CreateItemAsync<DbObjectPlayer>(player, key);
                }
                catch (Exception ex) when (ex.Message.Contains("Resource with specified id or name already exists"))
                {
                    ItemResponse<DbObjectPlayer> deletedResponse = await _container.DeleteItemAsync<DbObjectPlayer>(player.Id, key);
                    ItemResponse<DbObjectPlayer> playerDataResponse = await _container.CreateItemAsync<DbObjectPlayer>(player, key);
                    continue;
                }
            }

            return players;
        }

        private async Task<List<DbObjectPlayer>> RefreshPlayerList(Sport sport)
        {

            List<DbObjectPlayer> players = new();
            using HttpClient client = new HttpClient();
            string url = $"https://api.cbssports.com/fantasy/players/list?version=3.0&SPORT={sport}&response_format=JSON";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var respString = await response.Content.ReadAsStringAsync();
                ResponseObject responseObject = JsonConvert.DeserializeObject<ResponseObject>(respString);
                players = responseObject.body.players;

                players.ForEach(p =>
                {
                    p.Sport = sport;
                    p.PartitionKey = sport.ToString();
                });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return players;
        }

        public async Task<int> GetAverageAgeByPositionAndSportAsync(string position, Sport sport)
        {

            var sqlQueryText = @"SELECT VALUE AVG(c.Age) FROM c 
                                    WHERE c.Position = 'RF' 
                                    AND c.partitionKey = 'baseball' 
                                    AND c.Age > 0 
                                    AND c.Age < 100 
                                    AND c.Age != null";

            var queryDefinition = new QueryDefinition(sqlQueryText)
                .WithParameter("@position", position)
                .WithParameter("@sport", sport.ToString());

            var queryResultSetIterator = _container.GetItemQueryIterator<double>(queryDefinition);
            var response = await queryResultSetIterator.ReadNextAsync();
            var averageAge = response.FirstOrDefault();

            return Convert.ToInt32(averageAge);
        }
        public async Task<AverageAge> CalculateAverageAgesBySportAsync()
        {
            var averageAgesBySport = new AverageAge();
            var sqlQueryText = @"SELECT c.partitionKey AS Sport, AVG(c.Age) AS AverageAge 
                                FROM items c 
                                where c.Age != null and c.Age > 0
                                GROUP BY c.partitionKey";

            var queryDefinition = new QueryDefinition(sqlQueryText);

            var queryResultSetIterator = _container.GetItemQueryIterator<dynamic>(queryDefinition);

            while (queryResultSetIterator.HasMoreResults)
            {
                var response = await queryResultSetIterator.ReadNextAsync();

                foreach (var result in response)
                {
                    var sport = result["Sport"].ToObject<string>();
                    var averageAge = result["AverageAge"].ToObject<double>();

                    switch (sport)
                    {
                        case "football":
                            averageAgesBySport.Football = (int)averageAge;
                            break;
                        case "basketball":
                            averageAgesBySport.Basketball = (int)averageAge;
                            break;
                        case "baseball":
                            averageAgesBySport.Baseball = (int)averageAge;
                            break;
                    }
                }
            }

            return averageAgesBySport;
        }

        private async Task<List<DbObjectPlayer>> AdjustData(List<DbObjectPlayer> players)
        {
            //I only want to adjust the data we actually use.  So rather than do it in the refresh, I'll do it as needed. This does mean that a huge search will be slower.
            //This is a good place to do it since we need all of the data before we can do the average age calculations so there is no point trying to do it on the fly.

            //save the value once we calculate it.
            var averageAgesForPositions = new Dictionary<string, int>();


            foreach (var player in players)
            {
                if (player.Age > 0)
                {
                    //This value should be the difference between the age of the player vs the average age for the player’s position
                    if(averageAgesForPositions.ContainsKey(player.Position))
                    {
                        var avg = averageAgesForPositions[player.Position];
                        player.AveragePositionAgeDiff = player.Age - avg;
                    }
                    else
                    {
                        var averageAge = await GetAverageAgeByPositionAndSportAsync(player.Position, player.Sport);
                        averageAgesForPositions.Add(player.Position, averageAge);
                        player.AveragePositionAgeDiff = player.Age - averageAge;
                    }
                }

                var firstInitial = player.FirstName?.FirstOrDefault().ToString() ?? string.Empty;
                var lastInitial = player.LastName?.FirstOrDefault().ToString() ?? string.Empty;

                if (player.Sport == Sport.football)
                {
                    // For football players, set name_brief as the first initial and last name
                    player.NameBrief = $"{firstInitial}. {player.LastName}";
                }
                else if (player.Sport == Sport.basketball)
                {
                    // For basketball players, set name_brief as first name and last initial
                    player.NameBrief = $"{player.FirstName} {lastInitial}.";
                }
                else if (player.Sport == Sport.baseball)
                {
                    // For baseball players, set name_brief as the first initial and last initial
                    player.NameBrief = $"{firstInitial}. {lastInitial}.";
                }
            }

            return players;

        }

        private async Task<List<DbObjectPlayer>> SearchPlayersAsync(SearchQuery searchQuery)
        {
            var sb = new StringBuilder();
            var ageRange = ParseAgeRange(searchQuery.Age ?? string.Empty);

            //we need partition key, but if they didn't selecet a sport, we need to search all partitions
            sb.Append("SELECT * FROM Items c WHERE c.partitionKey = c.partitionKey");
           
            if(searchQuery?.Sport != null)
            {
                sb.Append(" AND c.partitionKey = @partitionKey");
            }

            var firstInitialLastName = searchQuery?.LastName?.Trim().FirstOrDefault().ToString().ToUpper() ?? string.Empty;
            if (!string.IsNullOrEmpty(firstInitialLastName))
            {
                sb.Append(" AND LEFT(c.LastName, 1) = @lastName");
            }

            if (!string.IsNullOrEmpty(searchQuery?.Position))
            {
                sb.Append(" AND c.Position = @position");
            }

            if (ageRange != null)
            {
                sb.Append(" AND (c.Age >= @lower_bound AND c.Age <= @upper_bound)");
            }
      

            var query = sb.ToString();
            var queryDefinition = new QueryDefinition(query)
                .WithParameter("@partitionKey", searchQuery?.Sport?.ToString())
                .WithParameter("@lastName", firstInitialLastName)
                .WithParameter("@position", searchQuery?.Position?.Trim() ?? string.Empty)
                .WithParameter("@lower_bound", ageRange?.Min() ?? 0)
                .WithParameter("@upper_bound", ageRange?.Max() ?? 0);
            
            var queryResultSetIterator = _container.GetItemQueryIterator<DbObjectPlayer>(queryDefinition);

            var matchedPlayers = new List<DbObjectPlayer>();

            while (queryResultSetIterator.HasMoreResults)
            {
                var response = await queryResultSetIterator.ReadNextAsync();
                matchedPlayers.AddRange(response.ToList());
            }

            matchedPlayers = await AdjustData(matchedPlayers);
            return matchedPlayers;
        }

        public static int?[] ParseAgeRange(string ageRange)
        {
            try
            {
                if(string.IsNullOrEmpty(ageRange))
                {
                    return new int?[] { 0, 0 };
                }   

                ageRange = ageRange.Trim().Replace(" ", "-");
                string[] range = ageRange.Split('-');
                List<int> result = range.Select(int.Parse).ToList();

                var min = (result.Min() > 0) ? result.Min() : 1;
                var max = (result.Max() < 100) ? result.Max() : 100; 
                var rv = new int?[] { min, max };

                return rv;
            }
            catch(Exception ex)
            {
                //this should be smarter - but if they tried to search on something that isn't an int, just return 0's
                return new int?[] { 0, 0};
            }
        }
    }
}
