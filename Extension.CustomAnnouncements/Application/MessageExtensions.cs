using Disqord;

namespace Extension.CustomAnnouncements.Application;

public static class MessageExtensions
{
    public static string ReplacePlaceholders(string message, Dictionary<string, string> placeholders)
    {
        placeholders["{@@everyone}"] = "@everyone";
        placeholders.Add("@everyone", "@everyone");
        placeholders.Add("@here", "@here");

        foreach (var placeholder in placeholders)
        {
            message = message.Replace($"{{{placeholder.Key}}}", Markdown.Bold(placeholder.Value), StringComparison.OrdinalIgnoreCase);
        }
        return message;
    }
}
