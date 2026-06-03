using bot_api.Models;

namespace bot_api.Interfaces;

public interface IAgentChat
{
    Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt);
}
