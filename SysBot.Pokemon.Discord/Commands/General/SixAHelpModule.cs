using Discord.Commands;
using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using System;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    //Channel Annonce & Change Section

    public class AnnounceModule : ModuleBase<SocketCommandContext>
    {
            public static readonly List<Action<string, string>> Announcers = new();

        private class AnnounceAction : ChannelAction<string, string>
        {
            public AnnounceAction(ulong id, Action<string, string> messager, string channel) : base(id, messager, channel)
            {
            }
        }

        private static readonly Dictionary<ulong, AnnounceAction> Channels = new();

        public static void RestoreAnnounce(DiscordSocketClient discord, DiscordSettings settings)
        {
            foreach (var ch in settings.ChannelAnnouncelist)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddAnnounceChannel(c, ch.ID);
            }

            LogUtil.LogInfo("Added Announcement channel(s) on Bot startup.", "Discord");
        }

        private static void AddAnnounceChannel(ISocketMessageChannel c, ulong cid)
        {
            void Announcer(string msg, string identity)
            {
                try
                {
                    var eb = new EmbedBuilder();
                    eb.WithTitle("Bot Announcement");
                    eb.WithColor(0,0,0);
                    eb.AddField("⠀", msg);
                    eb.WithTimestamp(DateTimeOffset.Now);
                    c.SendMessageAsync("⠀", false, eb.Build());

                    //c.SendMessageAsync(GetMessage(msg, identity));
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    LogUtil.LogSafe(ex, identity);
                }
            }

            Action<string, string> l = Announcer;
            Announcers.Add(l);

            static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";

            var entry = new AnnounceAction(cid, l, c.Name);
            Channels.Add(cid, entry);
        }

        private RemoteControlAccess GetReferencea(IChannel channel) => new()
        {
            ID = channel.Id,
            Name = channel.Name,
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };

        private static void Annie(string message, string identity)
        {
            foreach (var fwd in Announcers)
            {
                try
                {
                    fwd(message, identity);
                }
                catch (Exception ex)
                {
                    fwd(ex.ToString(), identity);
                }
            }
        }



        [Command("announceHere")]
        [Alias("AH", "ah")]
        [Summary("Makes the bot announce/change names of the current channel")]
        [RequireSudo]
        public async Task AddLogAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (Channels.TryGetValue(cid, out _))
            {
                await ReplyAsync("This channel is already in the Announce Channel List").ConfigureAwait(false);
                return;
            }

            AddAnnounceChannel(c, cid);

            // Add to discord global loggers (saves on program close)
            SysCordSettings.Settings.ChannelAnnouncelist.AddIfNew(new[] { GetReferencea(Context.Channel) });
            await ReplyAsync("Added this channel to announcement list!").ConfigureAwait(false);
        }

        [Command("Announce")]
        [Alias("A", "a")]
        [RequireOwner]
        public async Task AnnounceAsync([Summary("Makes an announcement in all linked channels")] string announcement)
        {
            Annie(announcement, " ");
        }

        [Command("Change")]
        [Alias("CH", "ch")]
        [RequireOwner]
        public async Task ChangerAsync([Summary("Changes the name of all linked channels")] string newname)
        {
            var client = Context.Client;
            var chanarray = SysCordSettings.HubConfig.Discord.ChannelAnnouncelist.List.ToArray();

            foreach (var chan in chanarray)
            {
                ulong.TryParse(chan.ID.ToString(), out var tchan);
                var tradechan = (ITextChannel)client.GetChannelAsync(tchan).Result;
                await tradechan.ModifyAsync(prop => prop.Name = $"{newname}");
                LogUtil.LogText("Attempted to change channel names");
            }
        }

        [Command("Off")]
        [Alias("off")]
        [RequireOwner]
        [Summary("Changes the name of all linked channels to botname offline X")]
        public async Task OffAsync()
        {
            var client = Context.Client;
            var chanarray = SysCordSettings.HubConfig.Discord.ChannelAnnouncelist.List.ToArray();

            foreach (var chan in chanarray)
            {
                ulong.TryParse(chan.ID.ToString(), out var tchan);
                var tradechan = (ITextChannel)client.GetChannelAsync(tchan).Result;
                await tradechan.ModifyAsync(prop => prop.Name = $"{client.CurrentUser.Username.ToString()}" + " offline❌");
                LogUtil.LogText("Attempted to turn channel names to offline");
            }
        }

        [Command("On")]
        [Alias("on")]
        [RequireOwner]
        public async Task OnAsync([Summary("Changes the name of all linked channels to botname gamemode online ✅")] string gamemode)
        {
            var client = Context.Client;
            var chanarray = SysCordSettings.HubConfig.Discord.ChannelAnnouncelist.List.ToArray();

            foreach (var chan in chanarray)
            {
                ulong.TryParse(chan.ID.ToString(), out var tchan);
                var tradechan = (ITextChannel)client.GetChannelAsync(tchan).Result;
                await tradechan.ModifyAsync(prop => prop.Name = $"{client.CurrentUser.Username.ToString()}" + $" {gamemode}✅");
                LogUtil.LogText("Attempted to turn channel names to offline");
            }
        }

        [Command("Yeet")]
        [Alias("yeet")]
        [Summary("Leaves the server the command is executed in")]
        [RequireOwner]
        public async Task YeetAsync()
        {
            var client = Context.Client;
            var guildylocks = client.Guilds.Equals(Context.Guild);
            await Context.Channel.SendMessageAsync("Yeetus My Fetus");
            await Context.Guild.LeaveAsync();

            LogUtil.LogText("I have left the server ");
        }

    //   [Command("Servers")]
    //   [Alias("SERVERS", "servers")]
    //   [Summary("Shows an embedded list of the servers the bot is in")]
    //   [RequireOwner]
    //   public async Task ServersAsync()
    //   {
    //       not yet functioning as intended
    //   }
    }
}