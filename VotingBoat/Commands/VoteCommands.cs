using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Qmmands;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;
using Color = SixLabors.ImageSharp.Color;

namespace VotingBoat.Commands
{
    internal sealed class VoteCommands : DiscordModuleBase
    {
        private readonly VoteBoat _voteBoat;

        public VoteCommands(VoteBoat voteBoat)
        {
            _voteBoat = voteBoat;
        }

        [Command("Ping")]
        [Description("Check bot's status.")]
        public Task PingAsync()
        {
            var latency = Math.Round(Context.Bot.Latency?.TotalMilliseconds ?? 0, 2);
            return ReplyAsync($":ping_pong: | **Je suis toujours là. `{latency}ms`**");
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
        public async Task ListAsync()
        {
            try
            {
                var colors = new (Color, Color)[]
                {
                    (Color.FromHex("007BFF"), Color.FromHex("268EFF")),
                    (Color.FromHex("DC3545"), Color.FromHex("E15360"))
                };

                var height = 30;
                var width = 1000;
                var border = 10;

                var nbVotesMax = _voteBoat.Database.VoteMessages.Values.Sum(x => x.VoteUserIds.Length > 0 ? x.VoteUserIds.Split(',').Length : 0);

                Image lastImage = null;
                var index = 0;
                foreach (var msg in _voteBoat.Database.VoteMessages.Values)
                {
                    var nbVotes = msg.VoteUserIds.Length > 0 ? msg.VoteUserIds.Split(',').Length : 0;

                    var img = CreateImageFor(msg.Name, nbVotes, nbVotesMax, height, width, border, colors[index].Item1, colors[index].Item2);

                    if (lastImage != null)
                    {
                        lastImage = AddImageToCurrent(lastImage, img);
                    }
                    else
                    {
                        lastImage = img;
                    }

                    if (++index == colors.Length)
                    {
                        index = 0;
                    }
                }

                var absenteisme = CreateImageFor("Absentéisme au vote", Context.Guild.MemberCount - nbVotesMax, Context.Guild.MemberCount, height, width, border, colors[index].Item1, colors[index].Item2);
                lastImage = AddImageToCurrent(lastImage, absenteisme);

                using var finalImage = ApplyBackgroundAndMargin(lastImage);
                var memStream = new MemoryStream();
                finalImage.Save(memStream, new PngEncoder());
                memStream.Position = 0;
                var attachment = new LocalAttachment(memStream, "liste.png");
                await ReplyAsync(attachment);
            }
            catch (Exception e)
            {
                await ReplyAsync("```" + e.Message + "\n" + e.StackTrace + "```");
                await ReplyAsync("```" + e.GetBaseException().Message + "\n" + e.GetBaseException().StackTrace + "```");
            }
        }

        private Image ApplyBackgroundAndMargin(Image image)
        {
            var finalImage = new Image<Rgba32>(image.Width + 20, image.Height + 10);
            finalImage.Mutate(x => x.BackgroundColor(Color.FromHex("36393F")));
            finalImage.Mutate(x => x.DrawImage(image, new Point(10, 0), 1f));
            return finalImage;
        }

        private Image AddImageToCurrent(Image image, Image imageToAdd)
        {
            var finalImage = new Image<Rgba32>(image.Width, image.Height + imageToAdd.Height + 20);
            finalImage.Mutate(x => x.DrawImage(image, new Point(0, 0), 1f));
            finalImage.Mutate(x => x.DrawImage(imageToAdd, new Point(0, image.Height + 20), 1f));
            return finalImage;
        }

        private Image CreateImageFor(string name, int nbVotes, int nbVotesMax, int height, int width, int border, Color backgroundColor, Color stripsColor)
        {
            var percents = (float)nbVotes / nbVotesMax;

            using Image image = new Image<Rgba32>(width, height);

            var pw = (int)(percents * width);

            if (pw > 0)
            {
                //coloring percent range
                image.Mutate(x => x.BackgroundColor(backgroundColor, new Rectangle(0, 0, pw, height)));

                //coloring strips
                for (var i = 0; i < pw - 25; i += 25)
                {
                    var x1 = new PointF(i, 0);
                    var x2 = new PointF(i + 7, 0);
                    var x3 = new PointF(i + height, height);
                    var x4 = new PointF(i + height - 7, height);

                    image.Mutate(x => x.FillPolygon(stripsColor, x1, x2, x3, x4));
                }
            }

            //coloring background
            image.Mutate(x => x.BackgroundColor(Color.FromHex("E9ECEF")));
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop
            }));

            //applying corners
            image.Mutate(x =>
            {
                var size = x.GetCurrentSize();
                var rect = new RectangularPolygon(-0.5f, -0.5f, border, border);
                var cornerTopLeft = rect.Clip(new EllipsePolygon(border - 0.5f, border - 0.5f, border));

                var rightPos = width - cornerTopLeft.Bounds.Width + 1;
                var bottomPos = height - cornerTopLeft.Bounds.Height + 1;

                var cornerTopRight = cornerTopLeft.RotateDegree(90).Translate(rightPos, 0);
                var cornerBottomLeft = cornerTopLeft.RotateDegree(-90).Translate(0, bottomPos);
                var cornerBottomRight = cornerTopLeft.RotateDegree(180).Translate(rightPos, bottomPos);

                var corners = new PathCollection(cornerTopLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);

                var graphicOptions = new GraphicsOptions(true)
                {
                    AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
                };

                x.Fill(graphicOptions, Rgba32.LimeGreen, corners);
            });

            //writing X.X%
            var font = SystemFonts.CreateFont("Calibri", 20);
            image.Mutate(x => x.DrawText($"{Math.Round(percents * 100, 2)}%", font, Color.Black, new PointF(width / 1.07f, (height - 20) / 2.0f)));

            //creating margin top of 34
            var finalImage = new Image<Rgba32>(width, height + 34);
            finalImage.Mutate(x => x.DrawImage(image, new Point(0, 34), 1f));

            //writing teamname and votes
            font = font.Family.CreateFont(30, FontStyle.Bold);
            finalImage.Mutate(x => x.DrawText($"{name} ({nbVotes}/{nbVotesMax} votes)", font, Color.FromHex("3498DB"/*"B31414"*/), new PointF(10, 34 / 6.0f)));

            return finalImage;
        }
    }
}
