using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public sealed class BotFactory7B : BotFactory<PB7>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PB7> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                => new PokeTradeBotLGPE(Hub, cfg),

            PokeRoutineType.RemoteControl => new RemoteControlBot(cfg),

            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                => true,

            PokeRoutineType.RemoteControl => true,

            _ => false,
        };
    }
}
