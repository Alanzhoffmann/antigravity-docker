using PersonalBot.Api.Endpoints;
using PersonalBot.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkflows();

var app = builder.Build();

app.MapGithubHookEndpoint();

app.Run("http://0.0.0.0:8080");
