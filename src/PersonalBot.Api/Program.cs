using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PersonalBot.Api;
using PersonalBot.Api.Chats;
using PersonalBot.Api.Endpoints;
using PersonalBot.Api.Interfaces;
using PersonalBot.Api.Options;
using PersonalBot.Tools;
using PersonalBot.Utils;

var builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(
    nameof(OllamaChat),
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
        client.BaseAddress = options.Url ?? throw new InvalidOperationException("Ollama url is missing");
        client.Timeout = Timeout.InfiniteTimeSpan;
    }
);

builder.Services.AddSingleton<IAgentChat, OllamaChat>();
builder.Services.AddSingleton<IAgentChat, AgyChat>();
builder.Services.AddSingleton<IAgentChat, NullChat>();
builder.Services.AddScoped<WebhookProcessor>();
builder.Services.AddPersonalBotTools();
builder.Services.AddUtils();

builder.Services.AddOptions<OllamaOptions>().BindConfiguration(OllamaOptions.SectionName);
builder.Services.AddOptions<AgyOptions>().BindConfiguration(AgyOptions.SectionName);

var app = builder.Build();

app.MapGithubHookEndpoint();

app.Run("http://0.0.0.0:8080");
