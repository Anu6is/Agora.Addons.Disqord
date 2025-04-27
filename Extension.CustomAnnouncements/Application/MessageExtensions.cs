using Disqord;

namespace Extension.CustomAnnouncements.Application;

public static class MessageExtensions
{
    public static string ReplacePlaceholders(string message, Dictionary<string, string> placeholders)
    {
        placeholders["{@@everyone}"] = Mention.Everyone;
        placeholders.Add("@everyone", Mention.Everyone);
        placeholders.Add("@here", Mention.Here);

        foreach (var placeholder in placeholders)
        {
            message = message.Replace($"{{{placeholder.Key}}}", Markdown.Bold(placeholder.Value), StringComparison.OrdinalIgnoreCase);
        }
        return message;
    }
}
