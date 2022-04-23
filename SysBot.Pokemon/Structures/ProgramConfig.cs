using SysBot.Base;

namespace SysBot.Pokemon
{
    public class ProgramConfig : BotList<PokeBotState>
    {
        public ProgramMode Mode { get; set; } = ProgramMode.LA;
        public PokeTradeHubConfig Hub { get; set; } = new();
    }

    public enum ProgramMode
    {
        LGPE = 0,
        SWSH = 1,
        BDSP = 2,
        LA = 3,
    }
}
