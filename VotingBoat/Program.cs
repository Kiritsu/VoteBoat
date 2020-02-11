using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Prefixes;
using Microsoft.Extensions.DependencyInjection;

namespace VotingBoat
{
    public sealed class Program
    {
        public static Task Main()
        {
            var container = BuildServiceProvider();
            var bot = container.GetRequiredService<VoteBoat>();
            return bot.InitializeAsync();
        }

        public static IServiceProvider BuildServiceProvider()
        {
            return new ServiceCollection()
                .AddSingleton<VoteBoat>()
                .AddSingleton(_ => new DefaultPrefixProvider().AddPrefix("v!"))
                .AddSingleton(container => new DiscordBotConfiguration
                {
                    ProviderFactory = _ => container
                })
                .AddSingleton(container => new DiscordBot(TokenType.Bot,
                    Environment.GetEnvironmentVariable("VOTEBOAT_TOKEN"),
                    container.GetRequiredService<DefaultPrefixProvider>(),
                    container.GetRequiredService<DiscordBotConfiguration>()))
                .BuildServiceProvider();
        }
    }
}
