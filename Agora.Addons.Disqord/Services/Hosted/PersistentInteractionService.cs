using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Extension;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Agora.Addons.Disqord
{
    public partial class PersistentInteractionService : DiscordBotService
    {
        private readonly ILogger<PersistentInteractionService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmporiaCacheService _cache;

        public PersistentInteractionService(DiscordBotBase bot,
                                            IServiceScopeFactory scopeFactory,
                                            IEmporiaCacheService cache,
                                            ILogger<PersistentInteractionService> logger) : base(logger, bot)
        {
            _cache = cache;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async ValueTask OnInteractionReceived(InteractionReceivedEventArgs args)
        {
            if (args.GuildId == null) return;

            if (args.Interaction is IComponentInteraction action)
                _logger.LogInformation("{Author} executed {Component} {ID}", action.Author, action.ComponentType, action.CustomId);

            if (args.Interaction is IModalSubmitInteraction modalSubmit)
            {

                var values = modalSubmit.Components.SelectMany(x => (x as IRowComponent).Components).Select(x => 
                {
                    var textComponent = x as TransientTextInputComponent;

                    return $"{textComponent.CustomId}: {textComponent.Value}";
                });

                _logger.LogInformation("{Author} submitted {ID} {Values}", modalSubmit.Author, modalSubmit.CustomId.Split(':')[0], values);
            }

            if (args.Interaction is IComponentInteraction interaction
                && interaction.ComponentType == ComponentType.Button
                && interaction.Message.Author.Id == Bot.CurrentUser.Id)
            {
                _logger.LogDebug("{Author} selected {button} in {guild}",
                                 interaction.Author.Name,
                                 interaction.CustomId,
                                 interaction.GuildId);

                if (interaction.CustomId.StartsWith("#")) return;

                using var scope = _scopeFactory.CreateScope();
                var interactionContext = scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(args);
                var loggerContext = scope.ServiceProvider.GetRequiredService<ILoggerContext>();

                SetLogContext(interactionContext, loggerContext);

                var roomId = await DetermineShowroomAsync(args);

                if (roomId == 0) return;
                if (!_modalRedirect.ContainsKey(interaction.CustomId) && !_confirmationRequired.ContainsKey(interaction.CustomId.Split(':').First()))
                    await interaction.Response().DeferAsync();

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var authorize = await AuthorizeInteractionAsync(args, interaction, roomId, scope, mediator);

                if (!authorize.IsSuccessful)
                {
                    await interaction.SendMessageAsync(
                            new LocalInteractionMessageResponse().WithIsEphemeral()
                                .AddEmbed(new LocalEmbed().WithColor(Color.Red).WithDescription(authorize.FailureReason)));
                    return;
                }

                if (!await ConfirmInteractionAsync(interaction)) return;

                var modalInteraction = await SendModalInteractionResponseAsync(interaction);

                var result = modalInteraction == null
                    ? Result.Success(HandleInteraction(interaction, roomId))
                    : await HandleModalInteraction(modalInteraction, roomId);

                if (!result.IsSuccessful)
                {
                    IUserInteraction userInteraction = modalInteraction is null ? interaction : modalInteraction;

                    await userInteraction.SendMessageAsync(
                            new LocalInteractionMessageResponse()
                                .WithIsEphemeral()
                                .AddEmbed(new LocalEmbed().WithColor(Color.Red).WithDescription(result.FailureReason)));
                }
                else
                {
                    var command = result.Data;

                    if (command is CreatePaymentCommand { Offer: not null } || command is CreateBidCommand { UseMinimum: false, UseMaximum: false })
                        scope.ServiceProvider.GetRequiredService<IAuthorizationService>().IsAuthorized = false;

                    await HandleInteractionResponseAsync(args, interaction, scope, modalInteraction, command, mediator);
                }
            }

            return;
        }

        private async Task HandleInteractionResponseAsync(InteractionReceivedEventArgs args,
                                                          IComponentInteraction interaction,
                                                          IServiceScope scope,
                                                          IModalSubmitInteraction modalInteraction,
                                                          IBaseRequest command,
                                                          IMediator mediator)
        {
            IResult result = null;
            try
            {
                if (command is not null) result = await mediator.Send(command) as IResult;

                var inError = result is not null && !result.IsSuccessful;

                if (modalInteraction is not null)
                {
                    if (inError)
                        await modalInteraction.SendMessageAsync(
                            new LocalInteractionMessageResponse()
                                .WithIsEphemeral()
                                .AddEmbed(new LocalEmbed().WithColor(Color.Red).WithDescription(result.FailureReason)));
                    else
                        await HandleResponse(modalInteraction);
                }
                else if (interaction is not null)
                {
                    if (inError)
                        await interaction.SendMessageAsync(
                            new LocalInteractionMessageResponse()
                                .WithIsEphemeral()
                                .AddEmbed(new LocalEmbed().WithColor(Color.Red).WithDescription(result.FailureReason)));
                    else
                        await HandleResponse(interaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing {command}", command?.GetType().Name ?? modalInteraction?.CustomId);
                await SendErrorResponseAsync(args, modalInteraction as IUserInteraction ?? interaction, scope, ex);
            }
        }

        private async Task<IResult> AuthorizeInteractionAsync(InteractionReceivedEventArgs args,
                                                           IComponentInteraction interaction,
                                                           ulong roomId,
                                                           IServiceScope scope,
                                                           IMediator mediator)
        {
            var request = AuthorizeInteraction(interaction, roomId);

            if (request == null) return Result.Success();

            try
            {
                var result = await mediator.Send(request) as IResult;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("{command} failed with error: {err}", request.GetType().Name, ex.Message);

                await SendErrorResponseAsync(args, interaction, scope, ex);
                return Result.Exception("Authorization Error: Unable to confirm user permissions.", ex);
            }
        }

        private async Task<bool> ConfirmInteractionAsync(IComponentInteraction interaction)
        {
            if (!_confirmationRequired.TryGetValue(interaction.CustomId.Split(':').First(), out var label)) return true;

            var locale = Bot.GetGuild(interaction.GuildId.Value).PreferredLocale;
            var components = interaction.Message.Components
                .Select(row =>
                {
                    return LocalComponent.Row(row.Components.Select(component =>
                    {
                        var button = (IButtonComponent)component;

                        return LocalComponent.Button(button.CustomId, TranslateButton(button.Label, locale))
                                             .WithStyle((LocalButtonComponentStyle)button.Style)
                                             .WithIsDisabled(button.IsDisabled);
                    }).ToArray());
                }).ToArray();

            await interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse()
            {
                Components = new[]
                {
                    LocalComponent.Row(
                        LocalComponent.Button($"{interaction.Message.Id}:deny", TranslateButton("Cancel", locale)).WithStyle(LocalButtonComponentStyle.Danger),
                        LocalComponent.Button($"{interaction.Message.Id}:confirm", TranslateButton(label, locale)).WithStyle(LocalButtonComponentStyle.Primary))
                }
            });

            var response = await Client
                .WaitForInteractionAsync<IComponentInteraction>(interaction.ChannelId,
                                                                button => button.CustomId.StartsWith(interaction.Message.Id.ToString())
                                                                       && button.AuthorId == interaction.AuthorId,
                                                                TimeSpan.FromSeconds(6),
                                                                Client.StoppingToken);

            var confirmed = response != null && response.CustomId.Contains("confirm");

            await interaction.Followup().ModifyResponseAsync(x => x.Components = components);

            return confirmed;
        }

        private async Task<ulong> DetermineShowroomAsync(InteractionReceivedEventArgs e)
        {
            ulong roomId;
            var emporium = await _cache.GetEmporiumAsync(e.GuildId.Value);

            if (emporium == null) return 0ul;

            if (emporium.Showrooms.Any(x => x.Id.Value.Equals(e.ChannelId.RawValue)))
            {
                roomId = e.ChannelId.RawValue;
            }
            else
            {
                roomId = Client.GetChannel(e.GuildId.Value, e.ChannelId) switch
                {
                    ITextChannel textChannel => textChannel.CategoryId.GetValueOrDefault(),
                    IThreadChannel threadChannel => threadChannel.ChannelId,
                    _ => 0
                };
            }

            return roomId;
        }

        private async ValueTask SendErrorResponseAsync(InteractionReceivedEventArgs e, IUserInteraction interaction, IServiceScope scope, Exception ex)
        {
            var message = ex switch
            {
                TimeoutException => null,
                ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                UnauthorizedAccessException unauthorizedAccessException => unauthorizedAccessException.Message,
                { } when ex.Message.IsNotNull() && ex.Message.Contains("interaction has already been", StringComparison.OrdinalIgnoreCase) => null,
                _ => "An error occured while processing this action. If this persists, please contact support."
            };

            if (message == null) return;

            await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

            var response = new LocalInteractionMessageResponse().WithIsEphemeral().WithContent(message);

            if (message.EndsWith("contact support."))
                response.WithComponents(LocalComponent.Row(LocalComponent.LinkButton("https://discord.gg/WmCpC8G", TranslateButton("Support Server", Bot.GetGuild(interaction.GuildId.Value).PreferredLocale))));

            await interaction.SendMessageAsync(response);

            return;
        }

        private async Task<IModalSubmitInteraction> SendModalInteractionResponseAsync(IComponentInteraction interaction)
        {
            if (_modalRedirect.ContainsKey(interaction.CustomId))
            {
                var timeout = TimeSpan.FromMinutes(14);
                var response = _modalRedirect[interaction.CustomId].Invoke(interaction);

                await interaction.Response().SendModalAsync(response);

                var reply = await Client.WaitForInteractionAsync(interaction.ChannelId,
                                                                 x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId,
                                                                 timeout,
                                                                 Client.StoppingToken);

                return reply?.Interaction as IModalSubmitInteraction;
            }

            return null;
        }

        private static void SetLogContext(DiscordInteractionContext interactionContext, ILoggerContext loggerContext)
        {
            var interaction = interactionContext.Interaction;

            loggerContext.ContextInfo.Add("ID", interaction.Id);

            if (interaction is IComponentInteraction component)
                loggerContext.ContextInfo.Add("Command", $"{component.Type}: {component.ComponentType} ({component.CustomId})");

            loggerContext.ContextInfo.Add("User", interaction.Author.GlobalName);
            loggerContext.ContextInfo.Add($"{interaction.Channel.Type}Channel", interaction.ChannelId);
            loggerContext.ContextInfo.Add("Guild", interaction.GuildId);
        }

        private string TranslateButton(string key, CultureInfo locale)
        {
            using var scope = _scopeFactory.CreateScope();
            var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
            localization.SetCulture(locale);

            return localization.Translate(key, "ButtonStrings");
        }
    }
}
