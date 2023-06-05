using SportData.Handlers;
using System.Reflection;
using SportData.Requests;

var builder = WebApplication.CreateBuilder(args);

// Add services to the _container.
builder.Services.AddRazorPages();

builder.Services.AddRazorPages();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();
app.MediatePost<SearchQuery>("/searchdata");
app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
