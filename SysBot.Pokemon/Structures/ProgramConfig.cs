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
        None = 0, // invalid
        LGPE = 1,
        SWSH = 2,
        BDSP = 3,
        LA = 4,
    }
}
