using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.IO;
using System.Linq;

namespace SysBot.Pokemon.Discord
{
    public class DiscordTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
    {
        private T Data { get; }
        private PokeTradeTrainerInfo Info { get; }
        private int Code { get; }
        private SocketUser Trader { get; }
        public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }
        public readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;

        public DiscordTradeNotifier(T data, PokeTradeTrainerInfo info, int code, SocketUser trader)
        {
            Data = data;
            Info = info;
            Code = code;
            Trader = trader;
        }

        public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
        {
            if (Data is PB7)
            {
                var receive = Data.Species == 0 ? string.Empty : $" ({Data.Nickname})";
                Trader.SendMessageAsync($"Initializing trade{receive}. Please be ready. Your code will be sent when I reach your spot in queue").ConfigureAwait(false);
            }
            else 
            {
                var receive = Data.Species == 0 ? string.Empty : $" ({Data.Nickname})";
                Trader.SendMessageAsync($"Initializing trade{receive}. Please be ready. Your code is **{Code:0000 0000}**.").ConfigureAwait(false);
            }
        }

        public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
        {
            if (Data is PB7)
            {
                var name = Info.TrainerName;
                var trainer = string.IsNullOrEmpty(name) ? string.Empty : $", {name}";
                Trader.SendMessageAsync($"I'm waiting for you{trainer}!").ConfigureAwait(false);
            }
            else
            {
                var name = Info.TrainerName;
                var trainer = string.IsNullOrEmpty(name) ? string.Empty : $", {name}";
                Trader.SendMessageAsync($"I'm waiting for you{trainer}! Your code is **{Code:0000 0000}**. My IGN is **{routine.InGameName2}**.").ConfigureAwait(false);
            }
        }

        public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
        {
            OnFinish?.Invoke(routine);
            Trader.SendMessageAsync($"Trade canceled: {msg}").ConfigureAwait(false);
        }

        public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
        {
            OnFinish?.Invoke(routine);
            var tradedToUser = Data.Species;
            var message = tradedToUser != 0 ? $"Trade finished. Enjoy your {(Species)tradedToUser}!" : "Trade finished!";
            Trader.SendMessageAsync(message).ConfigureAwait(false);
            if (result.Species != 0 && Hub.Config.Discord.ReturnPKMs)
                Trader.SendPKMAsync(result, "Here's what you traded me!").ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
        {
            Trader.SendMessageAsync(message).ConfigureAwait(false);
        }


        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, object file)
        {
            var filename = Path.GetFileName($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png");
            var codetext = System.IO.File.ReadAllText($"{System.IO.Directory.GetCurrentDirectory()}//codetext.txt");
            var emb = new EmbedBuilder()
                    .WithColor(0xFDFD96)
                    .WithTimestamp(DateTime.Now)
                    .WithTitle($"{codetext}")
                    .WithImageUrl($"attachment://{filename}")
                    .Build();
            //Trader.SendFileAsync($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png", null, false, emb).ConfigureAwait(false);
            Trader.SendFileAsync(filename, null, false, emb).ConfigureAwait(false);
        }



        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
        {
            if (message.ExtraInfo is SeedSearchResult r)
            {
                SendNotificationZ3(r);
                return;
            }

            var msg = message.Summary;
            if (message.Details.Count > 0)
                msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
            Trader.SendMessageAsync(msg).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
        {
            if (result.Species != 0 && (Hub.Config.Discord.ReturnPKMs || info.Type == PokeTradeType.Dump))
                Trader.SendPKMAsync(result, message).ConfigureAwait(false);
        }

        private void SendNotificationZ3(SeedSearchResult r)
        {
            var lines = r.ToString();

            var embed = new EmbedBuilder { Color = Color.LighterGrey };
            embed.AddField(x =>
            {
                x.Name = $"Seed: {r.Seed:X16}";
                x.Value = lines;
                x.IsInline = false;
            });
            var msg = $"Here are the details for `{r.Seed:X16}`:";
            Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
