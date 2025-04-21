using Agora.Addons.Disqord;
using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Gateway;
using Disqord.Rest;
using Extension.CustomAnnouncements.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Extension.CustomAnnouncements.Application;

public sealed class ManageAnnouncementsView : ViewBase
{
    private readonly ulong _guildId;
    private string _customMessage;
    private AnnouncementType _selectedAnnouncementType;
    private ISelectionComponentInteraction? _interaction;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string CustomizationExplanation = """
            Winner announcements consist of two components: **Text** and an **Embed**. 

            The **Embed** is fixed, however, the **Text** can be customized for your server. 
            When crafting a custom announcement, you can use placeholders in your message. 
            
            Placeholders will be replaced by actual data when the announcement is posted.

            """;

    private static readonly LocalEmbed ExplanationEmbed = new LocalEmbed()
                .WithDefaultColor()
                .WithTitle("Customize Announcements")
                .WithDescription(CustomizationExplanation + "​\u200b")
                .AddField("{owner}", Markdown.CodeBlock("The username of the owner (mentions/pings the user)"))
                .AddField("{winner} - for Result Logs ONLY", Markdown.CodeBlock("The username(s) of the winner(s) (mentions/pings the user(s))"))
                .AddField("{quantity}", Markdown.CodeBlock("The number of items in this transaction"))
                .AddField("{itemName}", Markdown.CodeBlock("The name of the item that was sold"))
                .AddField("{listingType}", Markdown.CodeBlock("The type of listing. Example: Live Auction"))
                .AddField("{forumPost} - for Result Logs ONLY", Markdown.CodeBlock("A link to the listing's forum post - only applicable to forum showrooms"))
                .AddField("{@RoleName} - for New Listing ONLY", Markdown.CodeBlock("A role to ping") + "​​​​​​​​​​​​​​​\u200b \u200b​")
                .AddField("Example", "Congratulations **{winner}**!\nYou just won **{quantity} {itemname}**.\nDM **{owner}** to claim your prize :tada:");

    private readonly List<LocalSelectionComponentOption> _options =
    [
        new LocalSelectionComponentOption().WithLabel($"{AnnouncementType.Default}")
                                           .WithEmoji(AgoraEmoji.RedCrossMark)
                                           .WithValue($"{(int)AnnouncementType.Default}")
                                           .WithDescription("Used when no custom message exists for a listing type"),
        new LocalSelectionComponentOption().WithLabel($"{AnnouncementType.Auction}")
                                           .WithEmoji(AgoraEmoji.RedCrossMark)
                                           .WithValue($"{(int)AnnouncementType.Auction}")
                                           .WithDescription("Custom message for Auction listings results"),
        new LocalSelectionComponentOption().WithLabel($"{AnnouncementType.Giveaway}")
                                           .WithEmoji(AgoraEmoji.RedCrossMark)
                                           .WithValue($"{(int)AnnouncementType.Giveaway}")
                                           .WithDescription("Custom message for Giveaway listings results"),
        new LocalSelectionComponentOption().WithLabel($"{AnnouncementType.Market}")
                                           .WithEmoji(AgoraEmoji.RedCrossMark)
                                           .WithValue($"{(int)AnnouncementType.Market}")
                                           .WithDescription("Custom message for Market listings results"),
        new LocalSelectionComponentOption().WithLabel($"{AnnouncementType.Trade}")
                                           .WithEmoji(AgoraEmoji.RedCrossMark)
                                           .WithValue($"{(int)AnnouncementType.Trade}")
                                           .WithDescription("Custom message for Trade listings resuts"),
        new LocalSelectionComponentOption().WithLabel("New Listing")
                                           .WithEmoji(AgoraEmoji.RedCrossMark)
                                           .WithValue($"{(int)AnnouncementType.Listing}")
                                           .WithDescription("Add a message for newly created listings")
    ];

    public ManageAnnouncementsView(ulong guildId, IEnumerable<Announcement> customAnnouncements, IServiceScopeFactory scopeFactory) : base(message => message.AddEmbed(ExplanationEmbed))
    {
        _scopeFactory = scopeFactory;

        foreach (var customAnnouncement in customAnnouncements)
            _options.First(options => options.Label.Value.Contains($"{customAnnouncement.AnnouncementType}")).Emoji = AgoraEmoji.GreenCheckMark;

        AddComponent(new SelectionViewComponent(SelectAnnouncementType)
        {
            Placeholder = "Select an announcement type to customize!",
            MaximumSelectedOptions = 1,
            Options = _options
        });

        _guildId = guildId;

        foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
        {
            button.Label = TranslateButton(button.Label!);
        }
    }

    public async ValueTask SelectAnnouncementType(SelectionEventArgs e)
    {
        if (e.SelectedOptions.Count == 0) return;

        var options = e.Selection.Options;
        var selectedOption = e.SelectedOptions[0];

        _selectedAnnouncementType = UpdateDefaultSelection(options, selectedOption);

        var scope = _scopeFactory.CreateScope();
        var announcementService = scope.ServiceProvider.GetRequiredService<CustomAnnouncementService>();
        var announcement = await announcementService.GetAnnouncementAsync(e.GuildId!.Value, _selectedAnnouncementType);

        if (!announcement.IsSuccessful && _selectedAnnouncementType != AnnouncementType.Default)
            announcement = await announcementService.GetAnnouncementAsync(e.GuildId!.Value, AnnouncementType.Default);

        if (_selectedAnnouncementType == AnnouncementType.Listing)
            _customMessage = announcement.Data ?? string.Concat("Use Edit to create a new listing message.\n Example: ", Mention.Everyone, " a new {listingType} is available!");
        else 
            _customMessage = announcement.Data ?? "{owner} | {winner}";

        var embed = new LocalEmbed()
            .WithColor(Color.Teal)
            .WithTitle($"{_selectedAnnouncementType} Announcement Message")
            .WithDescription(_customMessage);

        _interaction = e.Interaction;

        await _interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithEmbeds(embed));
    }

    [Button(Label = "Delete", Style = LocalButtonComponentStyle.Danger)]
    public async ValueTask DeleteMessage(ButtonEventArgs e)
    {
        await ClearResponseAsync();

        if (!await AnnouncementTypeSelected(e)) return;

        _customMessage = string.Empty;

        var selection = EnumerateComponents().OfType<SelectionViewComponent>().First();

        foreach (var option in selection.Options)
        {
            option.IsDefault = false;

            if (option.Label.Equals($"{_selectedAnnouncementType}")) option.Emoji = AgoraEmoji.RedCrossMark;
        }

        var scope = _scopeFactory.CreateScope();
        var announcementService = scope.ServiceProvider.GetRequiredService<CustomAnnouncementService>();

        var result = await announcementService.DeleteAnnouncementAsync(e.GuildId!.Value, _selectedAnnouncementType);
        var message = result.IsSuccessful ? result.Data : result.FailureReason;

        ReportChanges();

        await e.Interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent(message));
    }

    [Button(Label = "Edit", Style = LocalButtonComponentStyle.Primary)]
    public async ValueTask EditMessage(ButtonEventArgs e)
    {
        await ClearResponseAsync();

        if (!await AnnouncementTypeSelected(e)) return;

        var modal = await SendModalInteractionAsync(e);

        var selection = EnumerateComponents().OfType<SelectionViewComponent>().First();

        var options = selection.Options;
        var selectedOption = options.First(x => x.Value == $"{(int)_selectedAnnouncementType}");

        _selectedAnnouncementType = UpdateDefaultSelection(options, selectedOption);

        if (modal is null) return;

        _customMessage = GetCustomMessage(modal);

        selectedOption.Emoji = AgoraEmoji.GreenCheckMark;

        var scope = _scopeFactory.CreateScope();
        var announcementService = scope.ServiceProvider.GetRequiredService<CustomAnnouncementService>();
        var result = await announcementService.AddAnnouncementAsync(e.GuildId!.Value, _selectedAnnouncementType, _customMessage);
        var response = result.IsSuccessful ? result.Data : result.FailureReason;

        await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(response).WithIsEphemeral());

        ReportChanges();
    }

    [Button(Label = "Preview", Style = LocalButtonComponentStyle.Success)]
    public async ValueTask PreviewMessage(ButtonEventArgs e)
    {
        await ClearResponseAsync();

        if (!await AnnouncementTypeSelected(e)) return;

        var bot = e.Interaction.Client as DiscordBot;
        var title = "Sample Item";
        var quantity = Random.Shared.Next(1, 6).ToString();
        var winner = Mention.User(e.AuthorId);
        var owner = Mention.User(bot!.CurrentUser.Id);
        var listingType = $"Standard {(_selectedAnnouncementType == AnnouncementType.Default ? "Listing" : _selectedAnnouncementType)}";
        var placeholders = new Dictionary<string, string>
        {
            { "owner", owner },
            { "winner", winner },
            { "quantity", quantity },
            { "itemName", title },
            { "listingType", listingType },
            { "forumPost", Discord.MessageJumpLink(e.GuildId, e.ChannelId, e.Interaction.Message.Id) }
        };

        var guildRoles = await bot!.FetchRolesAsync(e.GuildId!.Value);
        var roles = guildRoles.ToDictionary(x => $"@{x.Name}", x => Mention.Role(x.Id));
        var message = MessageExtensions.ReplacePlaceholders(_customMessage, placeholders.Concat(roles).ToDictionary());
        var embed = new LocalEmbed().WithTitle($"{listingType} Claimed")
                            .WithDescription($"{Markdown.Bold($"{quantity} {title}")} for **xXx**")
                            .WithFooter("review this transaction | right-click -> apps -> review")
                            .AddInlineField("Owner", owner)
                            .AddInlineField("Claimed By", winner)
                            .WithColor(Color.Teal);
        
        await e.Interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent(message).AddEmbed(embed));
    }

    private async ValueTask<bool> AnnouncementTypeSelected(ButtonEventArgs e)
    {
        if (string.IsNullOrEmpty(_customMessage))
        {
            await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Select an announcement type!").WithIsEphemeral());

            return false;
        }

        return true;
    }

    [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary)]
    public ValueTask Close(ButtonEventArgs e) => default;

    protected override string GetCustomId(InteractableViewComponent component)
    {
        if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

        return base.GetCustomId(component);
    }

    private async Task ClearResponseAsync()
    {
        if (_interaction is not null)
        {
            try
            {
                await _interaction.Followup().DeleteResponseAsync();
            }
            catch (Exception)
            {
                //already removed
            }
        }

        _interaction = null;
    }

    private AnnouncementType UpdateDefaultSelection(IList<LocalSelectionComponentOption> options, LocalSelectionComponentOption selectedOption)
    {
        if (options.FirstOrDefault(option => option.IsDefault == true) is { } currentDefault)
        {
            currentDefault.IsDefault = false;
        }

        options.First(option => option.Value == selectedOption.Value).IsDefault = true;

        var value = int.Parse(selectedOption.Value.Value);

        ReportChanges();

        return (AnnouncementType)value;
    }

    private async Task<IModalSubmitInteraction?> SendModalInteractionAsync(ButtonEventArgs e)
    {
        var response = new LocalInteractionModalResponse()
            .WithCustomId(e.Interaction.Message.Id.ToString())
            .WithTitle($"Custom {_selectedAnnouncementType} Announcement")
            .WithComponents(LocalComponent.Row(new LocalTextInputComponent()
            {
                Style = TextInputComponentStyle.Paragraph,
                Label = "New Message",
                Placeholder = "Remember the placeholders - {owner} {winner} {quantity} {itemName} {listingType} {forumPost}",
                CustomId = e.Interaction.Message.Id.ToString(),
                IsRequired = true
            }));

        await e.Interaction.Response().SendModalAsync(response);

        var reply = await Menu.Interactivity.WaitForInteractionAsync(e.ChannelId, x =>
        {
            return x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId;
        },
        TimeSpan.FromMinutes(10), Menu.StoppingToken);

        return reply?.Interaction as IModalSubmitInteraction;
    }

    private static string GetCustomMessage(IModalSubmitInteraction modal)
    {
        var rows = modal.Components.OfType<IRowComponent>();
        var value = rows.First().Components.OfType<ITextInputComponent>().First().Value!;

        return value;
    }

    private string TranslateButton(string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        var bot = scope.ServiceProvider.GetRequiredService<DiscordBotBase>();

        localization.SetCulture(bot.GetGuild(_guildId)!.PreferredLocale);

        return localization.Translate(key, "ButtonStrings");
    }
}
