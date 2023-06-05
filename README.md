# SportsApp


	Make sure both projects are set to startup projects. Open appsettings.json and add the azure credentials (sent separately) Start solution

Open postman: Sent a POST to http://localhost:5214/search with the headers of Content-Type application/json and { "Sport": "baseball", "Position": "CF", "LastName": "Bader", "Age": "5-29" } in the body.

Searches can include baseball, basketball, and football and any combination of the 4 parameters. Age may be 1 or more numbers separated by a hyphen or space. The search will use the largest and smallest numbers as a range.
