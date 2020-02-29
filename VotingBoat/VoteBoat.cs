using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Events;
using Disqord.Logging;
using Disqord.Rest;
using VotingBoat.Commands;
using VotingBoat.Database;

namespace VotingBoat
{
    internal sealed class VoteBoat
    {
        private readonly DateTimeOffset _baseTime;
        private readonly List<string> _messages;

        private readonly DiscordBot _client;
        public InMemoryVoteDbContext Database { get; set; }
        public LocalEmoji OkHandEmoji { get; set; }

        public VoteBoat(DiscordBot client, InMemoryVoteDbContext database)
        {
            _baseTime = new DateTimeOffset(2020, 03, 08, 00, 00, 00, TimeSpan.FromHours(1)); ;
            _messages = new List<string>();
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

            if (e.Emoji.Name != OkHandEmoji.Name)
            {
                return;
            }

            if (!Database.VoteMessages.TryGetValue(e.Message.Id, out var voteMessage))
            {
                return;
            }

            var message = await e.Message.GetAsync();
            await message.RemoveMemberReactionAsync(e.User.Id, e.Emoji);

            if (user.CreatedAt > _baseTime)
            {
                _messages.Add($"[{DateTimeOffset.Now}] [Compte trop jeune] | Reaction by {user.Name}#{user.Discriminator} ({user.Id}).");
                return;
            }

            if (Database.HasAlreadyVoted(e.User.Id))
            {
                _messages.Add($"[{DateTimeOffset.Now}] [Déjà voté] | Reaction by {user.Name}#{user.Discriminator} ({user.Id}).");
                return;
            }

            if (voteMessage.VoteUserIds.Length > 0)
            {
                voteMessage.VoteUserIds += voteMessage.VoteUserIds[^1] == ',' ? "" : ",";
            }
            voteMessage.VoteUserIds += e.User.Id.RawValue;

            await Database.UpdateAsync();

            if (e.Channel is CachedGuildChannel chn && user is CachedMember mbr)
            {
                _messages.Add($"[{DateTimeOffset.Now}] [Vote] | Vote by {user.Name}#{user.Discriminator} ({user.Id}; createdat {user.CreatedAt}; joinedat {mbr.JoinedAt}).");
            }
        }

        private void OnMessageLogged(object sender, MessageLoggedEventArgs e)
        {
            Console.WriteLine(e);
        }

        private Task OnReady(ReadyEventArgs e)
        {
            _messages.Add($"[{DateTimeOffset.Now}] [Ready] | Just logged in.");

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (_messages.Count > 0)
                        {
                            await e.Client.SendMessageAsync(403632917118713868, string.Join("\n", _messages));
                            _messages.Clear();
                        }

                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                    catch (Exception e)
                    {
                        _client.Logger.Log(this, new MessageLoggedEventArgs(
                            "ReadyEvent", LogMessageSeverity.Critical, "Error in while(true)", e));
                    }
                }
            });

            e.Client.Logger.Log(this, new MessageLoggedEventArgs("VoteBoat",
                LogMessageSeverity.Information, "VoteBoat is ready."));

            return Task.CompletedTask;
        }
    }
}
