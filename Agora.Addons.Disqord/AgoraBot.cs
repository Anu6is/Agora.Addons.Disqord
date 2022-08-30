﻿using Agora.Addons.Disqord.Commands;
using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Commands.Interaction;
using Emporia.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;
using Qmmands.Default;
using Qmmands.Text;
using System.Reflection;

namespace Agora.Addons.Disqord
{
    internal class AgoraBot : DiscordBot
    {
        public AgoraBot(IOptions<DiscordBotConfiguration> options, ILogger<DiscordBot> logger, IServiceProvider services, DiscordClient client)
            : base(options, logger, services, client) { }

        protected override IEnumerable<Assembly> GetModuleAssemblies()
            => new[] { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() };

        protected override ValueTask<IResult> OnBeforeExecuted(IDiscordCommandContext context)
        {
            if (AgoraModuleBase.ShutdownInProgress || AgoraModuleBase.RebootInProgress)
                return Results.Failure("Services are presently unavailable, while entering maintenance mode.");

            return base.OnBeforeExecuted(context);
        }

        protected async override ValueTask OnFailedResult(IDiscordCommandContext context, IResult result)
        {
            if (result is CommandNotFoundResult) return;

            await base.OnFailedResult(context, result);

            await Services.GetRequiredService<UnhandledExceptionService>().CommandExecutionFailed(context, result);

            return;
        }

        protected override LocalMessageBase CreateFailureMessage(IDiscordCommandContext context)
        {
            if (context is IDiscordInteractionCommandContext)
                return new LocalInteractionMessageResponse() { IsEphemeral = true };

            return new LocalMessage().WithComponents(LocalComponent.Row(LocalComponent.LinkButton("https://discord.gg/WmCpC8G","Support Server")));
        }

        protected override string FormatFailureReason(IDiscordCommandContext context, IResult result)
        {
            if (result is ExceptionResult executionFailedResult)
                return executionFailedResult.Exception switch
                {
                    ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                    UnauthorizedAccessException unauthorizedAccessException => unauthorizedAccessException.Message,
                    _ => executionFailedResult.Exception.Message
                };

            return base.FormatFailureReason(context, result);
        }

        protected override bool FormatFailureMessage(IDiscordCommandContext context, LocalMessageBase message, IResult result)
        {
            static string FormatParameter(IParameter parameter)
            {
                var typeInformation = parameter.GetTypeInformation();
                var format = "{0}";
                if (typeInformation.IsEnumerable)
                {
                    format = "{0}[]";
                }
                else if (parameter is IPositionalParameter positionalParameter && positionalParameter.IsRemainder)
                {
                    format = "{0}…";
                }

                format = typeInformation.IsOptional
                    ? $"[{format}]"
                    : $"<{format}>";

                return string.Format(format, parameter.Name);
            }

            var reason = FormatFailureReason(context, result);
            if (reason == null)
                return false;

            var embed = new LocalEmbed().WithDescription(reason).WithColor(0x2F3136);
            var module = context.Command.Module as ApplicationModule;
            var parent = module?.Parent as ApplicationModule;
            var alias = $"{parent?.Alias} {module?.Alias} {context.Command.Name}".TrimStart();
            
            if (result is OverloadsFailedResult overloadsFailedResult)
            {
                foreach (var (overload, overloadResult) in overloadsFailedResult.FailedOverloads)
                {
                    var overloadReason = FormatFailureReason(context, overloadResult);
                    if (overloadReason == null)
                        continue;

                    embed.AddField($"Overload: {alias} {string.Join(' ', overload.Parameters.Select(FormatParameter))}", overloadReason);
                }
            }
            else if (context.Command != null)
            {
                embed.WithTitle($"Command: {alias} ");
            }

            message.AddEmbed(embed);
            message.WithAllowedMentions(LocalAllowedMentions.None);
            return true;
        }

        protected override ValueTask AddTypeParsers(DefaultTypeParserProvider typeParserProvider, CancellationToken cancellationToken)
        {
            typeParserProvider.AddParser(new StringValueTypeParser<ProductTitle>(75));
            typeParserProvider.AddParser(new StringValueTypeParser<HiddenMessage>(250));
            typeParserProvider.AddParser(new StringValueTypeParser<ProductDescription>(500));
            typeParserProvider.AddParser(new IntValueTypeParser<Stock>());
            typeParserProvider.AddParser(new DateTimeTypeParser());
            typeParserProvider.AddParser(new TimeSpanTypeParser());
            typeParserProvider.AddParser(new TimeTypeParser());

            return base.AddTypeParsers(typeParserProvider, cancellationToken);
        }
    }
}