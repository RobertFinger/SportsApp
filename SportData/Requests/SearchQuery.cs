﻿namespace SportData.Requests
{
    public class SearchQuery : IHttpRequest
    {
        public string Sport { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
        public string Age { get; set; }
    }
}
