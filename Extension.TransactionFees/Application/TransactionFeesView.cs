using Agora.Addons.Disqord;
using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Extension.TransactionFees.Application;

public sealed class TransactionFeesView : ViewBase
{
    private readonly TransactionFeeSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;

    public TransactionFeesView(TransactionFeeSettings settings, IServiceScopeFactory scopeFactory) : base(message => message.AddEmbed(new LocalEmbed()
            .WithTitle("Server Fee Configurations")
            .WithDescription("**Note:**\n- Fixed fees are subtracted when the listing is created.\n- Percentage fees are subtracted when the listing is sold.\n- Trades are exempt from fees")
            .AddField($"{GetEmoji(settings.ServerFee?.Value > 0)} Listing Fees: {settings.ServerFee?.ToString() ?? "None"}",
                      "When set, item owners pay a fee for creating a listing")
            .AddField($"{GetEmoji(settings.BrokerFee?.Value > 0)} Broker Fees: {settings.BrokerFee?.ToString() ?? "None"}",
                      "When set, item owners pay a fee if a Broker is used")
            .AddField($"{GetEmoji(settings.AllowEntryFee)} Entry Fees: {(settings.AllowEntryFee ? "Enabled" : "Disabled")}",
                      "Determines if owners can require a entry fee to participate in a listing")
            .WithDefaultColor()))
    {
        _settings = settings;
        _scopeFactory = scopeFactory;

        AddComponent(new ButtonViewComponent(x => default) { Label = "Close" });

        foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
        {
            if (button.Label!.Equals("Entry Fees"))
                button.Label = settings?.AllowEntryFee is true ? "Disable Entry Fees" : "Enable Entry Fees";
        }
    }

    private static string GetEmoji(bool value) => value ? AgoraEmoji.GreenCheckMark.ToString() : AgoraEmoji.RedCrossMark.ToString();

    [Button(Label = "Listing Fees", Style = LocalButtonComponentStyle.Primary)]
    public async ValueTask ListingFees(ButtonEventArgs e)
    {
        var modal = await SendModalInteractionAsync("Set Listing Fee", e);

        if (modal is null) return;

        var fee = await GetFeeSubmissionAsync(modal);

        if (fee is null) return;

        _settings.ServerFee = fee;

        await SaveSettingsAsync();

        await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Listing fee successfully updated").WithIsEphemeral());

        RefreshView();
    }

    [Button(Label = "Broker Fees", Style = LocalButtonComponentStyle.Primary)]
    public async ValueTask BrokerFees(ButtonEventArgs e)
    {
        var modal = await SendModalInteractionAsync("Set Broker Fee", e);

        if (modal is null) return;

        var fee = await GetFeeSubmissionAsync(modal);

        if (fee is null) return;

        _settings.BrokerFee = fee;

        await SaveSettingsAsync();

        await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Broker fee successfully updated").WithIsEphemeral());

        RefreshView();
    }

    [Button(Label = "Entry Fees", Style = LocalButtonComponentStyle.Primary)]
    public async ValueTask EntryFees(ButtonEventArgs e)
    {
        _settings.AllowEntryFee = !_settings.AllowEntryFee;

        await SaveSettingsAsync();

        e.Button.Label = _settings?.AllowEntryFee is true ? "Disable Entry Fees" : "Enable Entry Fees";

        RefreshView();
    }

    private static async Task<TransactionFee?> GetFeeSubmissionAsync(IModalSubmitInteraction modal)
    {
        var rows = modal.Components.OfType<IRowComponent>();
        var value = rows.First().Components.OfType<ITextInputComponent>().First().Value!;
        var isPercentage = value.EndsWith('%');

        if (isPercentage) value = value.TrimEnd('%', ' ');

        if (int.TryParse(value, out var fee) && fee >= 0)
            return TransactionFee.Create(fee, isPercentage);

        await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Please enter a valid number value").WithIsEphemeral());

        return null;
    }

    private async Task<IModalSubmitInteraction?> SendModalInteractionAsync(string title, ButtonEventArgs e)
    {
        var response = new LocalInteractionModalResponse()
            .WithCustomId(e.Interaction.Message.Id.ToString())
            .WithTitle(title)
            .WithComponents(LocalComponent.Row(new LocalTextInputComponent()
            {
                Style = TextInputComponentStyle.Short,
                Label = "Fixed Amount or Percentage",
                CustomId = title.Replace(" ", ""),
                Placeholder = "example: 5 or 5%",
                MaximumInputLength = 20,
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

    private async Task SaveSettingsAsync()
    {
        var scope = _scopeFactory.CreateScope();
        var data = scope.ServiceProvider.GetRequiredService<IDataAccessor>();

        await data.Transaction<GenericRepository<TransactionFeeSettings>>().UpdateAsync(_settings);
    }

    private void RefreshView()
    {
        MessageTemplate = message =>
        {
            message.AddEmbed(new LocalEmbed()
                .WithTitle("Server Fee Configurations")
                .WithDescription("**Note:**\n- Fixed fees are subtracted when the listing is created.\n- Percentage fees are subtracted when the listing is sold.\n- Trades are exempt from fees")
                .AddField($"{GetEmoji(_settings.ServerFee?.Value > 0)} Listing Fees: {_settings.ServerFee?.ToString() ?? "None"}",
                          "When set, item owners pay a fee for creating a listing")
                .AddField($"{GetEmoji(_settings.BrokerFee?.Value > 0)} Broker Fees: {_settings.BrokerFee?.ToString() ?? "None"}",
                          "When set, item owners pay a fee if a Broker is used")
                .AddField($"{GetEmoji(_settings.AllowEntryFee)} Entry Fees: {(_settings.AllowEntryFee ? "Enabled" : "Disabled")}",
                          "Determines if owners can require a entry fee to participate in a listing")
                .WithDefaultColor());
        };

        ReportChanges();
    }

    protected override string GetCustomId(InteractableViewComponent component)
    {
        if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

        return base.GetCustomId(component);
    }
}
