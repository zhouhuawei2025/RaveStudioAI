using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace RaveStudioAI.Common;

public static class AIHelper
{
    public static async Task<string> AskAsync(
        string instruction,
        string input,
        CancellationToken cancellationToken = default)
    {
        // 请求开始时复制配置，避免用户在请求途中切换 Profile 影响本次调用。
        var config = AIConfigStore.Current.Copy();

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException($"AI 配置“{config.Name}”尚未填写 API Key。");
        }

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(config.Url),
            NetworkTimeout = TimeSpan.FromMinutes(5)
        };

        IChatClient client = new OpenAI.Chat.ChatClient(
                config.Model,
                new ApiKeyCredential(config.ApiKey),
                options)
            .AsIChatClient();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, instruction),
            new(ChatRole.User, input)
        };

        var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Text;
    }
}
