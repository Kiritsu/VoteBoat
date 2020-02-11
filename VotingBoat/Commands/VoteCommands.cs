using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Qmmands;

namespace VotingBoat.Commands
{
    internal sealed class VoteCommands : DiscordModuleBase
    {
        private readonly VoteBoat _voteBoat;

        public VoteCommands(VoteBoat voteBoat)
        {
            _voteBoat = voteBoat;
        }

        [Command("Add")]
        [Description("Starts listening to reactions of a message.")]
        public async Task AddAsync(CachedGuildChannel rawChannel, Snowflake messageId, string name)
        {
            if (!(rawChannel is CachedTextChannel channel))
            {
                await ReplyAsync(":frowning: | **Le channel spécifié n'est pas un channel textuel valide.**");
                return;
            }

            var message = await channel.GetMessageAsync(messageId);
            var customReason = RestRequestOptions.FromReason($"{Context.User.Name}#{Context.User.Discriminator} ({Context.User.Id}) | Vote message bound.");
            await message.ClearReactionsAsync(options: customReason);
            await message.AddReactionAsync(_voteBoat.OkHandEmoji);

            await _voteBoat.Database.GetOrAddAsync(messageId, name);

            await ReplyAsync(":ok_hand: | **Ce message sera désormais ouvert aux votes, s'il ne l'était pas déjà.**");
        }

        [Command("Remove")]
        [Description("Stops listening to reactions of a message.")]
        public async Task RemoveAsync(Snowflake messageId)
        {
            await _voteBoat.Database.RemoveAsync(messageId);
            await ReplyAsync(":ok_hand: | **Ce message n'est plus ouvert aux votes (si c'était le cas) et sa 'progression' a été perdue.**");
        }

        [Command("List")]
        [Description("Lists every message and their 'scoring'.")]
        public Task ListAsync()
        {
            var values = _voteBoat.Database.VoteMessages.Values;
            return ReplyAsync(string.Join("\n", values.Select(x => $"**{x.Name}** (`{x.VoteMessageId}`) | {(x.VoteUserIds.Length > 0 ? x.VoteUserIds.Split(',').Length : 0)} votes.")));
        }

        [Command("Ping")]
        [Description("Check bot's status.")]
        public Task PingAsync()
        {
            var latency = Math.Round(Context.Bot.Latency?.TotalMilliseconds ?? 0, 2);
            return ReplyAsync($":ping_pong: | **Je suis toujours là. `{latency}ms`**");
        }
    }
}
