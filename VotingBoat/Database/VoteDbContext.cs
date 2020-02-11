using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace VotingBoat.Database
{
    internal sealed class InMemoryVoteDbContext
    {
        private readonly IServiceProvider _container;

        public ConcurrentDictionary<ulong, VoteMessage> VoteMessages { get; set; }

        public InMemoryVoteDbContext(IServiceProvider container)
        {
            _container = container;
        }

        public async Task InitializeAsync()
        {
            using var scope = _container.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<VoteDbContext>();

            VoteMessages = new ConcurrentDictionary<ulong, VoteMessage>(
                await context.VoteMessages.ToDictionaryAsync(x => x.VoteMessageId, y => y));
        }

        public bool HasAlreadyVoted(Snowflake userId)
        {
            return VoteMessages.Values.Any(x => x.VoteUserIds.Contains(userId.RawValue.ToString()));
        }

        public async Task<VoteMessage> GetOrAddAsync(ulong voteMessageId, string name)
        {
            if (VoteMessages.TryGetValue(voteMessageId, out var current))
            {
                return current;
            }

            var voteMessage = new VoteMessage
            {
                VoteMessageId = voteMessageId,
                VoteUserIds = "",
                Name = name
            };

            using var scope = _container.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<VoteDbContext>();

            await context.VoteMessages.AddAsync(voteMessage);
            await context.SaveChangesAsync();

            VoteMessages.TryAdd(voteMessageId, voteMessage);

            return voteMessage;
        }

        public async Task RemoveAsync(ulong voteMessageId)
        {
            if (!VoteMessages.TryGetValue(voteMessageId, out var current))
            {
                return;
            }

            using var scope = _container.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<VoteDbContext>();

            context.VoteMessages.Remove(current);
            await context.SaveChangesAsync();

            VoteMessages.TryRemove(voteMessageId, out _);
        }

        public async Task UpdateAsync()
        {
            using var scope = _container.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<VoteDbContext>();

            context.VoteMessages.UpdateRange(VoteMessages.Select(x => x.Value));
            await context.SaveChangesAsync();
        }
    }

    internal sealed class VoteDbContext : DbContext
    {
        public DbSet<VoteMessage> VoteMessages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=vote.db");
        }
    }

    internal sealed class VoteMessage
    {
        /// <summary>
        ///     Id of the message that is tracked for votes.
        /// </summary>
        public ulong VoteMessageId { get; set; }

        /// <summary>
        ///     List of the user ids that voted for that message. These are separated with ','.
        /// </summary>
        public string VoteUserIds { get; set; }

        /// <summary>
        ///     Name given to that vote message.
        /// </summary>
        public string Name { get; set; }
    }
}
