using System;
using System.Reflection;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Events;
using Disqord.Logging;
using VotingBoat.Commands;
using VotingBoat.Database;

namespace VotingBoat
{
    internal sealed class VoteBoat
    {
        private readonly DiscordBot _client;
        public InMemoryVoteDbContext Database { get; set; }
        public LocalEmoji OkHandEmoji { get; set; }

        public VoteBoat(DiscordBot client, InMemoryVoteDbContext database)
        {
            _client = client;
            Database = database;
        }

        public async Task InitializeAsync()
        {
            OkHandEmoji = new LocalEmoji("👌");

            _client.Ready += OnReady;
            _client.ReactionAdded += OnReactionAdded;
            _client.Logger.MessageLogged += OnMessageLogged;

            _client.AddModule<VoteCommands>();

            await Database.InitializeAsync();

            await _client.RunAsync();
        }

        private async Task OnReactionAdded(ReactionAddedEventArgs e)
        {
            var user = await e.User.GetAsync();
            if (user.IsBot)
            {
                return;
            }

            if (!Database.VoteMessages.TryGetValue(e.Message.Id, out var voteMessage))
            {
                return;
            }

            if (e.Emoji.Name != OkHandEmoji.Name)
            {
                return;
            }

            var message = await e.Message.GetAsync();
            await message.RemoveMemberReactionAsync(e.User.Id, e.Emoji);

            if (Database.HasAlreadyVoted(e.User.Id))
            {
                return;
            }

            if (voteMessage.VoteUserIds.Length > 0)
            {
                voteMessage.VoteUserIds += voteMessage.VoteUserIds[^1] == ',' ? "" : ",";
            }
            voteMessage.VoteUserIds += e.User.Id.RawValue;

            await Database.UpdateAsync();
        }

        private void OnMessageLogged(object sender, MessageLoggedEventArgs e)
        {
            Console.WriteLine(e);
        }

        private Task OnReady(ReadyEventArgs e)
        {
            e.Client.Logger.Log(this, new MessageLoggedEventArgs("VoteBoat",
                LogMessageSeverity.Information, "VoteBoat is ready."));

            return Task.CompletedTask;
        }
    }
}
