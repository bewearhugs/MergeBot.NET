using System.ComponentModel;
using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Drawing;
using System.Linq;
using PKHeX.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;
using System.Collections;
using System.Collections.Generic;
using Discord;
using System.Diagnostics;


namespace SysBot.Pokemon
{
    public class LGTradeBotSettings
    {
        private const string LGTradeBot = nameof(LGTradeBot);
        public override string ToString() => "Trade Bot Settings";
       

        [Category(LGTradeBot), Description("The channel(s) the bot will be accepting commands in separated by a comma, no spaces at all.")]
        public string LGTradeBotchannel { get; set; } = string.Empty;

        [Category(LGTradeBot), Description("Turn this setting on to have the bot update discord channels for when it is online/offline when you press start or stop")]

        public bool channelchanger { get; set; } = false;

        [Category(LGTradeBot), Description("The name of your discord trade bot channel")]
        public string channelname { get; set; } = string.Empty;

        [Category(LGTradeBot), Description("MGDB folder path")]
        public string mgdbpath { get; set; } = string.Empty;
        [Category(LGTradeBot), Description("Turn on to Distribute")]
        public bool distribution { get; set; } = false;

        public static Action<List<PokeTradeBotLGPE.pictocodes>>? CreateSpriteFile { get; set; }
    }
}
