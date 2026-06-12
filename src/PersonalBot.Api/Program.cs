using PersonalBot.Api.Endpoints;
using PersonalBot.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackgroundServices();

var app = builder.Build();

app.MapGithubHookEndpoint();

app.Run("http://0.0.0.0:8080");
