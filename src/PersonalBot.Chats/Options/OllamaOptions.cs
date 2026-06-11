namespace PersonalBot.Chats.Options;

internal class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string? Model { get; set; } = "qwen3.5:9b";
    public Uri? Url { get; set; } = new Uri("http://localhost:11434/");
}
