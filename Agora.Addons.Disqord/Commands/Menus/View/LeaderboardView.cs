using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Features.Queries;
using Agora.Shared.Persistence.Specifications.Filters;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class LeaderboardView : ViewBase
    {
        private IDiscordGuildSettings _settings;
        private PagedResponse<LeaderboardResponse> _response;

        public LeaderboardView(IDiscordGuildSettings settings, PagedResponse<LeaderboardResponse> response)
            : base(message => LeaderboardMessage(response, message, settings.DefaultCurrency))
        {
            _settings = settings;
            _response = response;

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Label == "Previous") button.IsDisabled = response.PageNumber == 1;
                if (button.Label == "Next") button.IsDisabled = response.PageNumber == response.TotalPages;

                if (response.TotalPages <= 1 && button.Label != "Close") RemoveComponent(button);
            }
        }

        [Button(Label = "Previous", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public async ValueTask GoBack(ButtonEventArgs e)
        {
            var bot = Menu.Client as AgoraBot;
            var filter = new LeaderboardFilter(new EmporiumId(_settings.GuildId)) { PageNumber = _response.PageNumber - 1, IsPagingEnabled = true };

            using var scope = bot.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            _response = await mediator.Send(new GetEmporiumLeaderboardQuery(filter));

            MessageTemplate = message => LeaderboardMessage(_response, message, _settings.DefaultCurrency);

            ReportChanges();

            return;
        }

        [Button(Label = "Next", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public async ValueTask GoForward(ButtonEventArgs e)
        {
            var bot = Menu.Client as AgoraBot;
            var filter = new LeaderboardFilter(new EmporiumId(_settings.GuildId)) { PageNumber = _response.PageNumber + 1, IsPagingEnabled = true };

            using var scope = bot.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            _response = await mediator.Send(new GetEmporiumLeaderboardQuery(filter));

            MessageTemplate = message => LeaderboardMessage(_response, message, _settings.DefaultCurrency);

            ReportChanges();

            return;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 4, Row = 4)]
        public async ValueTask CloseView(ButtonEventArgs e) => await Task.Delay(TimeSpan.FromMilliseconds(500));

        private static LocalMessageBase LeaderboardMessage(PagedResponse<LeaderboardResponse> response, LocalMessageBase message, Currency currency)
        {
            return message.AddEmbed(
                new LocalEmbed()
                    .WithTitle("📈 Leaderboard")
                    .WithDescription(response.TotalDataCount == 0 ? "Nothing to see here..." : string.Empty)
                    .WithFooter($"Page {response.PageNumber} of {response.TotalPages}")
                    .WithFields(PopulateLeaderboard(response, currency))
                    .WithDefaultColor());
        }

        private static string[] Awards = new string[] { "🏆", "🥈", "🥉" };
        private static IEnumerable<LocalEmbedField> PopulateLeaderboard(PagedResponse<LeaderboardResponse> response, Currency currency)
        {
            var fields = new List<LocalEmbedField>();
            var badges = response.PageNumber == 1 ? 3 : 0;
            var topThree = response.Data.Take(badges).ToArray();

            for (int i = 0; i < topThree.Length; i++)
            {
                var user = topThree[i];
                fields.Add(new LocalEmbedField()
                      .WithName("\u200b")
                      .WithValue($"{Awards[i]}{Mention.User(user.UserReference)} -> {Money.Create(user.Balance, currency)}")
                      .WithIsInline(false));
            }

            foreach (var user in response.Data.Skip(badges))
            {
                fields.Add(new LocalEmbedField()
                      .WithName("\u200b")
                      .WithValue($"{Mention.User(user.UserReference)} -> {Money.Create(user.Balance, currency)}")
                      .WithIsInline(false));
            }

            return fields;
        }

        public override ValueTask UpdateAsync()
        {
            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Label == "Previous") button.IsDisabled = _response.PageNumber == 1;
                if (button.Label == "Next") button.IsDisabled = _response.PageNumber == _response.TotalPages;
            }

            return base.UpdateAsync();  
        }

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent && buttonComponent.Label == "Close") return buttonComponent.Label;

            return base.GetCustomId(component);
        }
    }
}
