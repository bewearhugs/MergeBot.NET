using PKHeX.Core;
using PKHeX.Core.Searching;
using PKHeX.Core.AutoMod;
using SysBot.Base;
using System;
using System.Drawing;
using System.Linq;
using PKHeX.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsLGPE;
using System.Collections;
using System.Collections.Generic;
using Discord;
using System.Diagnostics;
using System.Net.Sockets;

namespace SysBot.Pokemon
{

    public class PokeTradeBotLGPE : PokeRoutineExecutor7LGPE
    {
        public bool ShouldWaitAtBarrier { get; private set; }
        public static Action<List<pictocodes>>? CreateSpriteFile { get; set; }

        public static void generatebotsprites(List<pictocodes> code)
        {
            
            var func = CreateSpriteFile;
            if (func == null)
                return;
            func.Invoke(code);
        }

        public static SAV7b sav = new();
        public static PB7 pkm = new();
        public static PokeTradeHub<PB7>? Hub;
        public static Queue discordname = new();
        public static Queue pictocode = new();
        public static Queue Channel = new();
        public static Queue discordID = new();
        public static Queue tradepkm = new();
        public static int initialloop = 0;
        int passes = 0;

        public PokeTradeBotLGPE(PokeTradeHub<PB7> hub, PokeBotState cfg) : base(cfg)
        {

            Hub = hub;
            TradeSettings = hub.Config.Trade;
        }

        protected virtual (PokeTradeDetail<PB7>? detail, uint priority) GetTradeData(PokeRoutineType type)
        {
            if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
                return (detail, priority);
            if (Hub.Queues.TryDequeueLedy(out detail))
                return (detail, PokeTradePriorities.TierFree);
            return (null, PokeTradePriorities.TierFree);
        }

        public override async Task MainLoop(CancellationToken token)
        {
            APILegality.AllowBatchCommands = true;
            APILegality.AllowTrainerOverride = true;
            APILegality.ForceSpecifiedBall = true;
            APILegality.SetMatchingBalls = true;
            Legalizer.EnableEasterEggs = false;

            Log("Identifying trainer data of the host console.");
            sav = await LGIdentifyTrainer(token).ConfigureAwait(false);
            Log("Starting main TradeBot loop.");
            Config.IterateNextRoutine();
            var task = Config.CurrentRoutineType switch
            {
                PokeRoutineType.FlexTrade => DoTrades(sav,token),
                _ => DoNothing(token)
            };
            await SetController(token);
            Log("Controller(s) connected");
            await task.ConfigureAwait(false);
            await DetachController(token);
            Hub.Bots.Remove(this);

        }

        private const int InjectBox = 0;
        private const int InjectSlot = 0;

        public async Task DoNothing(CancellationToken token)
        {
            int waitCounter = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
            {
                if (waitCounter == 0)
                    Log("No task assigned. Waiting for new task assignment.");
                waitCounter++;
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
        }
        private static readonly byte[] BlackPixel = // 1x1 black pixel
       {
            0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        };

        private void UpdateBarrier(bool shouldWait)
        {
            if (ShouldWaitAtBarrier == shouldWait)
                return; // no change required

            ShouldWaitAtBarrier = shouldWait;
            if (shouldWait)
            {
                Hub.BotSync.Barrier.AddParticipant();
                Log($"Joined the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
            else
            {
                Hub.BotSync.Barrier.RemoveParticipant();
                Log($"Left the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
        }

        private async Task DoTrades(SAV7b sav, CancellationToken token)
        {
            var type = Config.CurrentRoutineType;
            int waitCounter = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == type)
            {
                var (detail, priority) = GetTradeData(type);
                if (detail is null)
                {
                    continue;
                }
                waitCounter = 0;

                detail.IsProcessing = true;
                string tradetype = $" ({detail.Type})";
                Log($"Starting next {type}{tradetype} Bot Trade. Getting data...");
                Hub.Config.Stream.StartTrade(this, detail, Hub);
                Hub.Queues.StartTrade(this, detail);

                await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
            }
        }

        private async Task PerformTrade(SAV7b sav, PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, CancellationToken token)
        {
            PokeTradeResult result;
            try
            {
                result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
                if (result == PokeTradeResult.Success)
                    return;
            }
            catch (SocketException socket)
            {
                Log(socket.Message);
                result = PokeTradeResult.ExceptionConnection;
                throw; // let this interrupt the trade loop. re-entering the trade loop will recheck the connection.
            }
            catch (Exception e)
            {
                Log(e.Message);
                result = PokeTradeResult.ExceptionInternal;
            }
        }

        private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV7b sav, PokeTradeDetail<PB7> poke, CancellationToken token)
        {
            //Press B a few times to prime on 1st start and back out if still on a menu somehow
            await Click(B, 1000, token);
            await Click(B, 1000, token);
            await Click(B, 1000, token);

            Stopwatch btimeout = new();

            //If barrier set synch bots
            UpdateBarrier(poke.IsSynchronized);

            //Initialize this data as trade
            poke.TradeInitialize(this);
            var toSend = poke.TradeData;


            if (toSend.Species != 0)
            {
                //If species != null write to Box
                var SlotSize = 260;
                var GapSize = 380;
                var read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                overworld = read[0];
                uint GetBoxOffset(int box) => 0x533675B0;
                uint GetSlotOffset(int box, int slot) => GetBoxOffset(box) + (uint)((SlotSize + GapSize) * slot);
                var slotofs = GetSlotOffset(1, 0);
                var StoredLength = SlotSize - 0x1C;
                await Connection.WriteBytesAsync(toSend.EncryptedBoxData.Slice(0, StoredLength), BoxSlot1, token);
                await Connection.WriteBytesAsync(toSend.EncryptedBoxData.SliceEnd(StoredLength), (uint)(slotofs + StoredLength + 0x70), token);

                /// SPECIFIC TRADE NO DISTRIB
                if (poke.Type != PokeTradeType.Random)
                {
                    Log("Starting Trade Type: Specific");

                    //Code Handler?
                    var code = new List<pictocodes>();
                    for (int i = 0; i <= 2; i++)
                    {
                        code.Add((pictocodes)Util.Rand.Next(10));
                    }

                    //SpriteDrawer
                    generatebotsprites(code);
                    var code0 = System.Drawing.Image.FromFile($"{System.IO.Directory.GetCurrentDirectory()}//code0.png");
                    var code1 = System.Drawing.Image.FromFile($"{System.IO.Directory.GetCurrentDirectory()}//code1.png");
                    var code2 = System.Drawing.Image.FromFile($"{System.IO.Directory.GetCurrentDirectory()}//code2.png");
                    var finalpic = Merge(code0, code1, code2);
                    finalpic.Save($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png");

                    string codetext = $"**{code[0]}, {code[1]}, {code[2]}**";

                    //code notifier will go here once i figure it out
                    try
                    {
                        poke.SendNotification(this, codetext);
                    }
                    catch (Exception ex)
                    {
                        Log($"{ex}");
                    }

                    //Write Specific Pokemon to Box
                    await Connection.WriteBytesAsync(toSend.EncryptedBoxData.Slice(0, StoredLength), BoxSlot1, token);
                    await Connection.WriteBytesAsync(toSend.EncryptedBoxData.SliceEnd(StoredLength), (uint)(slotofs + StoredLength + 0x70), token);
                    await SetController(token);
                    for (int i = 0; i < 3; i++)
                        await Click(A, 1000, token);
                    read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                    while (read[0] != overworld)
                    {

                        await Click(B, 1000, token);
                        read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                    }
                    await Click(X, 2000, token).ConfigureAwait(false);
                    Log("opening menu");
                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                    {
                        await Click(B, 2000, token);
                        await Click(X, 2000, token);
                    }
                    Log("selecting communicate");
                    await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) == waitingtotradescreen)
                    {

                        await Click(A, 1000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen2)
                        {
                            read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            while (read[0] != overworld)
                            {

                                await Click(B, 1000, token);
                                read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            }
                            await Click(X, 2000, token).ConfigureAwait(false);
                            Log("opening menu");
                            while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                            {
                                await Click(B, 2000, token);
                                await Click(X, 2000, token);
                            }
                            Log("selecting communicate");
                            await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                        }


                    }
                    await Task.Delay(2000);
                    Log("selecting faraway connection");

                    await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Click(A, 10000, token).ConfigureAwait(false);

                    await Click(A, 1000, token).ConfigureAwait(false);

                    Log("Entering Link Code");
                    foreach (pictocodes pc in code)
                    {
                        if ((int)pc > 4)
                        {
                            await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                        if ((int)pc <= 4)
                        {
                            for (int i = (int)pc; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            for (int i = (int)pc - 5; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }
                        await Click(A, 200, token).ConfigureAwait(false);
                        await Task.Delay(500).ConfigureAwait(false);
                        if ((int)pc <= 4)
                        {
                            for (int i = (int)pc; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, -30000, 0, 0, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            for (int i = (int)pc - 5; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, -30000, 0, 0, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }

                        if ((int)pc > 4)
                        {
                            await SetStick(SwitchStick.RIGHT, 0, 30000, 0, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                        continue;
                    }

                    await Task.Delay(3000);
                    btimeout.Restart();

                    var nofind = false;
                    while (await LGIsinwaitingScreen(token))
                    {
                        await Task.Delay(100);
                        if (btimeout.ElapsedMilliseconds >= 45_000)
                        {
                            await Click(B, 1000, token);
                            Log("User not found");
                            nofind = true;
                            read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            while (read[0] != overworld)
                            {
                                await Click(B, 1000, token);
                                read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            }
                            await Click(B, 1000, token);
                            await Click(B, 1000, token);
                            await Click(B, 1000, token);
                            await Click(B, 1000, token);
                        }
                    }
                    if (nofind)
                    {
                        System.IO.File.Delete($"{System.IO.Directory.GetCurrentDirectory()}/Block.png");
                        discordID.Dequeue();
                        discordname.Dequeue();
                        Channel.Dequeue();
                        tradepkm.Dequeue();
                        await Click(B, 1000, token);
                        return PokeTradeResult.NoTrainerFound;

                    }


                    Log("User Found");
                    await Task.Delay(10000);

                    System.IO.File.Delete($"{System.IO.Directory.GetCurrentDirectory()}/Block.png");

                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen)
                    {
                        await Click(A, 1000, token);
                    }

                    Log("waiting on trade screen");
                    await Task.Delay(15_000).ConfigureAwait(false);
                    await Click(A, 200, token).ConfigureAwait(false);
                    Log("trading...");
                    await Task.Delay(15000);
                    while (await LGIsInTrade(token))
                        await Click(A, 1000, token);

                    Log("Trade should be completed, exiting box");
                    passes = 0;
                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) != menuscreen)
                    {
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(B, 2000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(A, 2000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(B, 2000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(B, 2000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        if (passes >= 15)
                        {
                            Log("handling trade evolution");
                            for (int i = 0; i < 7; i++)
                            {
                                await Click(A, 1000, token);
                            }
                        }
                        passes++;
                    }
                    btimeout.Restart();
                    int acount = 4;
                    Log("spamming b to get back to overworld");
                    read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                    passes = 0;
                    while (read[0] != overworld)
                    {

                        await Click(B, 1000, token);
                        read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                        if (passes >= 20)
                        {
                            Log("handling trade evolution");
                            for (int i = 0; i < 7; i++)
                            {
                                await Click(A, 1000, token);
                            }
                        }
                        passes++;
                    }
                    await Click(B, 1000, token);
                    await Click(B, 1000, token);
                    Log("done spamming b");

                    return PokeTradeResult.Success;

                    Log("Nothing to do, waiting for next trade...");
                }





                /// DISTRIBUTION TRADE

                while (Hub.Config.Distribution.DistributeWhileIdle)
                {
                    Log("Starting Trade Type: Distribution");

                    var dcode = new List<pictocodes>();
                    for (int i = 0; i <= 2; i++)
                    {

                        dcode.Add(pictocodes.Pikachu);

                    }
                    //write dist pokemon (hopefully doesnt mess with specific trades
                    var dpkm = toSend;
                    await Connection.WriteBytesAsync(dpkm.EncryptedBoxData.Slice(0, StoredLength), BoxSlot1, token);
                    await Connection.WriteBytesAsync(dpkm.EncryptedBoxData.SliceEnd(StoredLength), (uint)(slotofs + StoredLength + 0x70), token);

                    System.IO.File.WriteAllText($"{System.IO.Directory.GetCurrentDirectory()}//LGPEDistrib.txt", $"LGPE Giveaway: Shiny {(Species)pkm.Species}");

                    await SetController(token);
                    for (int i = 0; i < 3; i++)
                        await Click(A, 1000, token);
                    read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                    while (read[0] != overworld)
                    {

                        await Click(B, 1000, token);
                        read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                    }

                    await Click(X, 2000, token).ConfigureAwait(false);
                    Log("opening menu");
                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                    {
                        await Click(B, 2000, token);
                        await Click(X, 2000, token);
                    }
                    Log("selecting communicate");
                    await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) == menuscreen)
                    {
                        await Click(A, 1000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen2)
                        {
                            read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            while (read[0] != overworld)
                            {

                                await Click(B, 1000, token);
                                read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            }
                            await Click(X, 2000, token).ConfigureAwait(false);
                            Log("opening menu");
                            while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                            {
                                await Click(B, 2000, token);
                                await Click(X, 2000, token);
                            }
                            Log("selecting communicate");
                            await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                    }
                    await Task.Delay(2000);
                    Log("selecting faraway connection");


                    await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Click(A, 10000, token).ConfigureAwait(false);

                    await Click(A, 1000, token).ConfigureAwait(false);

                    Log("Entering distribution Link Code");

                    foreach (pictocodes pc in dcode)
                    {
                        if ((int)pc > 4)
                        {
                            await SetStick(SwitchStick.RIGHT, 0, -30000, 100, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                        if ((int)pc <= 4)
                        {
                            for (int i = (int)pc; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, 30000, 0, 100, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            for (int i = (int)pc - 5; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, 30000, 0, 100, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }
                        await Click(A, 200, token).ConfigureAwait(false);
                        await Task.Delay(500).ConfigureAwait(false);
                        if ((int)pc <= 4)
                        {
                            for (int i = (int)pc; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, -30000, 0, 100, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            for (int i = (int)pc - 5; i > 0; i--)
                            {
                                await SetStick(SwitchStick.RIGHT, -30000, 0, 100, token).ConfigureAwait(false);
                                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(500).ConfigureAwait(false);
                            }
                        }

                        if ((int)pc > 4)
                        {
                            await SetStick(SwitchStick.RIGHT, 0, 30000, 100, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                    }

                    Log("Searching for distribution user");

                    btimeout.Restart();
                    var dnofind = false;
                    while (await LGIsinwaitingScreen(token))
                    {
                        await Task.Delay(100);
                        if (btimeout.ElapsedMilliseconds >= 45_000)
                        {

                            Log("User not found");
                            dnofind = true;
                            read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            while (read[0] != overworld)
                            {
                                await Click(B, 1000, token);
                                read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                            }
                        }
                    }
                    if (dnofind == true)
                        continue;
                    await Task.Delay(10000);

                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen)
                    {
                        await Click(A, 1000, token);
                    }
                    Log("waiting on trade screen");



                    await Task.Delay(15_000);
                    await Click(A, 200, token).ConfigureAwait(false);
                    Log("Distribution trading...");
                    await Task.Delay(15000);

                    while (await LGIsInTrade(token))
                        await Click(A, 1000, token);


                    Log("Trade should be completed, exiting box");
                    passes = 0;
                    while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) != menuscreen)
                    {
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(B, 1000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(A, 1000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(B, 1000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;
                        await Click(B, 1000, token);
                        if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                            break;

                        if (passes == 30)
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                await Click(A, 1000, token);
                            }
                        }
                        passes++;
                    }

                    btimeout.Restart();
                    int dacount = 4;
                    Log("spamming b to get back to overworld");
                    read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                    passes = 0;
                    while (read[0] != overworld)
                    {
                        await Click(B, 1000, token);
                        read = await SwitchConnection.ReadBytesMainAsync(ScreenOff, 1, token);
                        if (passes == 30)
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                await Click(A, 1000, token);
                            }
                        }
                        passes++;

                    }
                    await Click(B, 1000, token);
                    await Click(B, 1000, token);
                    Log("done spamming b");
                    await Task.Delay(2500);
                    initialloop++;
                    return PokeTradeResult.Success;
                }

                if (toSend.Species == 0)
                {
                    Log("No Valid Poke detected");
                }
                
            }
            return PokeTradeResult.NoTrainerFound;
        }

        public static Bitmap Merge(System.Drawing.Image firstImage, System.Drawing.Image secondImage, System.Drawing.Image thirdImage)
        {
            if (firstImage == null)
            {
                throw new ArgumentNullException("firstImage");
            }

            if (secondImage == null)
            {
                throw new ArgumentNullException("secondImage");
            }

            if (thirdImage == null)
            {
                throw new ArgumentNullException("thirdImage");
            }

            int outputImageWidth = firstImage.Width + secondImage.Width + thirdImage.Width + 2;

            int outputImageHeight = firstImage.Height;

            Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(outputImage))
            {
                graphics.DrawImage(firstImage, new Rectangle(0, 0, firstImage.Width, firstImage.Height),
                    new Rectangle(new Point(), firstImage.Size), GraphicsUnit.Pixel);
                graphics.DrawImage(secondImage, new Rectangle(50, 0, secondImage.Width, secondImage.Height),
                    new Rectangle(new Point(), secondImage.Size), GraphicsUnit.Pixel);
                graphics.DrawImage(thirdImage, new Rectangle(100, 0, thirdImage.Width, thirdImage.Height),
                    new Rectangle(new Point(), thirdImage.Size), GraphicsUnit.Pixel);
            }

            return outputImage;
        }
        public enum pictocodes
        {
            Pikachu,
            Eevee,
            Bulbasaur,
            Charmander,
            Squirtle,
            Pidgey,
            Caterpie,
            Rattata,
            Jigglypuff,
            Diglett
        }

        private readonly TradeSettings TradeSettings;
        public override async Task HardStop()
        {
            await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
        }

        public override Task<PB7> ReadPokemon(ulong offset, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<PB7> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<PB7> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<PB7> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }

}
