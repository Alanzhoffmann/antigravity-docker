using PersonalBot.Api;
using PersonalBot.Api.Endpoints;
using PersonalBot.Chats;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChats();

builder.Services.AddScoped<WebhookProcessor>();

var app = builder.Build();

app.MapGithubHookEndpoint();

app.Run("http://0.0.0.0:8080");
