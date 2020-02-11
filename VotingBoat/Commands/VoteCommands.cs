using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Disqord.Bot;
using Qmmands;

namespace VotingBoat.Commands
{
    public sealed class VoteCommands : DiscordModuleBase
    {
        [Command("Ping")]
        [Description("Check bot's status.")]
        public Task PingAsync()
        {
            var latency = Math.Round(Context.Bot.Latency?.TotalMilliseconds ?? 0, 2);
            return ReplyAsync($":ping_pong: | **Je suis toujours là. `{latency}ms`**");
        }
    }
}
