using System;
using System.Reflection;
using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Events;
using Disqord.Logging;

namespace VotingBoat
{
    internal sealed class VoteBoat
    {
        private readonly DiscordBot _client;

        public VoteBoat(DiscordBot client)
        {
            _client = client;
        }

        internal async Task InitializeAsync()
        {
            _client.Ready += OnReady;
            _client.Logger.MessageLogged += OnMessageLogged;

            _client.AddModules(Assembly.GetExecutingAssembly());

            await _client.RunAsync();
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
