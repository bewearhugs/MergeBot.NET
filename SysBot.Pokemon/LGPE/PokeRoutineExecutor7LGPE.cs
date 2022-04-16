using PKHeX.Core;
using SysBot.Base;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsLGPE;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor7LGPE : PokeRoutineExecutor<PB7>
    {
        public string InGameName { get; private set; } = "tiny.cc/bwhd";
        protected PokeDataOffsetsLGPE Offsets { get; } = new();
        protected PokeRoutineExecutor7LGPE(PokeBotState cfg) : base(cfg) { }

        public new LanguageID GameLang { get; private set; }
        public new GameVersion Version { get; private set; }

        public override void SoftStop() => Config.Pause();

        public new async Task Click(SwitchButton b, int delayMin, int delayMax, CancellationToken token) =>
            await Click(b, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);

        public new async Task SetStick(SwitchStick stick, short x, short y, int delayMin, int delayMax, CancellationToken token) =>
            await SetStick(stick, x, y, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);


        public async Task<PB7> LGReadPokemon(uint offset, CancellationToken token, int size = EncryptedSize, bool heap = true)
        {
            byte[] data;
            if (heap == true)
                data = await Connection.ReadBytesAsync(offset, size, token).ConfigureAwait(false);
            else
                data = await SwitchConnection.ReadBytesMainAsync(offset, size, token).ConfigureAwait(false);
            return new PB7(data);
        }
        public async Task<PB7> LGReadPokemon(ulong offset, CancellationToken token, int size = EncryptedSize)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PB7(data);
        }

        public async Task<PB7?> LGReadUntilPresent(uint offset, int waitms, int waitInterval, CancellationToken token, int size = EncryptedSize, bool heap = true)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await LGReadPokemon(offset, token, size, heap).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }
        public async Task<PB7?> LGReadUntilPresent(ulong offset, int waitms, int waitInterval, CancellationToken token, int size = EncryptedSize)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await LGReadPokemon(offset, token, size).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        public string GetInGameName()
        {
            return InGameName;
        }

        public async Task<SAV7b> LGIdentifyTrainer(CancellationToken token)
        {
            Log("Grabbing trainer data of host console...");
            SAV7b sav = await LGGetFakeTrainerSAV(token).ConfigureAwait(false);
            GameLang = (LanguageID)sav.Language;
            Version = sav.Version;
            InGameName = sav.OT;
            Connection.Label = $"{InGameName}-{sav.DisplayTID:000000}";
            Log($"{Connection.Name} identified as {Connection.Label}, using {GameLang}.");

            return sav;
        }

        public static void DumpPokemon(string folder, string subfolder, PKM pk)
        {
            if (!Directory.Exists(folder))
                return;
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            var fn = Path.Combine(dir, Util.CleanFileName(pk.FileName));
            File.WriteAllBytes(fn, pk.DecryptedPartyData);
            LogUtil.LogInfo($"Saved file: {fn}", "Dump");
        }

        public async Task<SAV7b> LGGetFakeTrainerSAV(CancellationToken token)
        {
            SAV7b lgpe = new SAV7b();

            byte[] dest = lgpe.Blocks.Status.Data;
            int startofs = lgpe.Blocks.Status.Offset;
            byte[]? data = await Connection.ReadBytesAsync(TrainerData, TrainerSize, token).ConfigureAwait(false);
            data.CopyTo(dest, startofs);
            return lgpe;
        }

        public async Task<bool> LGIsInTitleScreen(CancellationToken token) => !((await SwitchConnection.ReadBytesMainAsync(IsInTitleScreen, 1, token).ConfigureAwait(false))[0] == 1);
        public async Task<bool> LGIsInBattle(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInBattleScenario, 1, token).ConfigureAwait(false))[0] > 0;
        public async Task<bool> LGIsInCatchScreen(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInOverworld, 1, token).ConfigureAwait(false))[0] != 0;
        public async Task<bool> LGIsinwaitingScreen(CancellationToken token) => BitConverter.ToUInt32(await SwitchConnection.ReadBytesMainAsync(waitingscreen, 4, token).ConfigureAwait(false), 0) == 0;
        public async Task<bool> LGTradeButtonswait(CancellationToken token) => string.Format("0x{0:X8}", BitConverter.ToUInt32(await SwitchConnection.ReadBytesMainAsync(tradebuttons, 4, token), 0)) == "0x00000000";
        public async Task<bool> LGIsInTrade(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInTrade, 1, token).ConfigureAwait(false))[0] != 0;
        public async Task<bool> LGIsGiftFound(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsGiftFound, 1, token).ConfigureAwait(false))[0] > 0;
        public async Task<uint> LGEncounteredWild(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(CatchingSpecies, 2, token).ConfigureAwait(false), 0);
        public async Task<GameVersion> LGWhichGameVersion(CancellationToken token)
        {
            byte[] data = await Connection.ReadBytesAsync(LGGameVersion, 1, token).ConfigureAwait(false);
            if (data[0] == 0x01)
                return GameVersion.GP;
            else if (data[0] == 0x02)
                return GameVersion.GE;
            else
                return GameVersion.Invalid;
        }

        public async Task<bool> LGIsNatureTellerEnabled(CancellationToken token) => (await Connection.ReadBytesAsync(NatureTellerEnabled, 1, token).ConfigureAwait(false))[0] == 0x04;
        public async Task<Nature> LGReadWildNature(CancellationToken token) => (Nature)BitConverter.ToUInt16(await Connection.ReadBytesAsync(WildNature, 2, token).ConfigureAwait(false), 0);
        public async Task LGEnableNatureTeller(CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(0x04), NatureTellerEnabled, token).ConfigureAwait(false);
        public async Task LGEditWildNature(Nature target, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)target), WildNature, token).ConfigureAwait(false);
        public async Task<uint> LGReadSpeciesCombo(CancellationToken token) =>
            BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
        public async Task<uint> LGReadComboCount(CancellationToken token) =>
            BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
        public async Task LGEditSpeciesCombo(uint species, CancellationToken token) =>
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(species), await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
        public async Task LGEditComboCount(uint count, CancellationToken token) =>
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(count), await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
        public async Task<long> LGCountMilliseconds(PokeTradeHubConfig config, CancellationToken token)
        {
            long WaitMS = 2500;
            bool stuck = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            byte[] data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
            byte[] comparison = data;
            do
            {
                data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
                if (stopwatch.ElapsedMilliseconds > WaitMS)
                    stuck = true;
            } while (data.SequenceEqual(comparison) && stuck == false && !token.IsCancellationRequested);
            if (!stuck)
            {
                stopwatch.Restart();
                comparison = data;
                do
                {
                    data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
                } while (data == comparison && !token.IsCancellationRequested);
                return stopwatch.ElapsedMilliseconds;
            }
            else
                return 0;
        }

        public async Task LGOpenGame(PokeTradeHubConfig config, CancellationToken token)
        {
            // Open game.
            await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (config.Timings.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (await LGIsInTitleScreen(token).ConfigureAwait(false))
            {
                if (stopwatch.ElapsedMilliseconds > 6000)
                    await DetachController(token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);
            }
            Log("Game started.");
        }

        public async Task<ulong> ParsePointer(String pointer, CancellationToken token)
        {
            var ptr = pointer;
            uint finadd = 0;
            if (!ptr.EndsWith("]"))
                finadd = Util.GetHexValue(ptr.Split('+').Last());
            var jumps = ptr.Replace("main", "").Replace("[", "").Replace("]", "").Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries);
            if (jumps.Length == 0)
            {
                Log("Invalid Pointer");
                return 0;
            }

            var initaddress = Util.GetHexValue(jumps[0].Trim());
            ulong address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesMainAsync(initaddress, 0x8, token).ConfigureAwait(false), 0);
            foreach (var j in jumps)
            {
                var val = Util.GetHexValue(j.Trim());
                if (val == initaddress)
                    continue;
                if (val == finadd)
                {
                    address += val;
                    break;
                }
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + val, 0x8, token).ConfigureAwait(false), 0);
            }
            return address;
        }
        public async Task CleanExit(IBotStateSettings settings, CancellationToken token)
        {
            if (settings.ScreenOff)
            {
                Log("Turning on screen.");
                await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            }
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
        }

    }
}