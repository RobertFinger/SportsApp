using MediatR;
using Newtonsoft.Json;
using SportsApp.Models;
using SportsApp.Requests;

namespace SportsApp.Handlers
{
    public class SearchHandler : IRequestHandler<SearchParams, IResult>
    {
        public SearchHandler()
        {
            
        }
        public async Task<IResult> Handle(SearchParams request, CancellationToken cancellationToken)
        {

            using HttpClient client = new HttpClient();
            string url = "http://localhost:5166/searchdata";
            HttpContent content = JsonContent.Create(request);

            HttpResponseMessage response = await client.PostAsync(url, content);

            response.EnsureSuccessStatusCode();

            var respString = await response.Content.ReadAsStringAsync();
            var responseObject = JsonConvert.DeserializeObject<ResponseObject>(respString);
            List<Player> players = responseObject.Players;

            return Results.Ok(new
            {
                message = players
            });
        }
    }
}
 