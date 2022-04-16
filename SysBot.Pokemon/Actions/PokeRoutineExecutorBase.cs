﻿using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutorBase : SwitchRoutineExecutor<PokeBotState>
    {
        protected PokeRoutineExecutorBase(IConsoleBotManaged<IConsoleConnection, IConsoleConnectionAsync> cfg) : base(cfg)
        {
        }

        public LanguageID GameLang { get; private set; }
        public GameVersion Version { get; private set; }
        public string InGameName2 { get; private set; } = "SysBot.NET";

        public override string GetSummary()
        {
            var current = Config.CurrentRoutineType;
            var initial = Config.InitialRoutine;
            if (current == initial)
                return $"{Connection.Label} - {initial}";
            return $"{Connection.Label} - {initial} ({current})";
        }

        protected void InitSaveData(SaveFile sav)
        {
            GameLang = (LanguageID)sav.Language;
            Version = sav.Version;
            InGameName2 = sav.OT;
            Connection.Label = $"{InGameName2}-{sav.DisplayTID:000000}";
            Log($"{Connection.Name} identified as {Connection.Label}, using {GameLang}.");
        }

        protected bool IsValidTrainerData() => GameLang is (> 0 and <= LanguageID.ChineseT) && InGameName2.Length > 0 && Version > 0;

        public override void SoftStop() => Config.Pause();

        public async Task Click(SwitchButton b, int delayMin, int delayMax, CancellationToken token) =>
            await Click(b, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);

        public async Task SetStick(SwitchStick stick, short x, short y, int delayMin, int delayMax, CancellationToken token) =>
            await SetStick(stick, x, y, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);
    }
}
