﻿using Core;
using Core.Logs;
using Core.models.enums;
using Core.models.enums.errors;
using Core.models.room;
using Core.server;
using Game.data.model;
using Game.data.utils;
using Game.global.serverpacket;
using System;
using System.Collections.Generic;

namespace Game.global.clientpacket
{
    public class BATTLE_TIMERSYNC_REC : ReceiveGamePacket
    {
        private float unk0;
        private uint TimeRemaining;
        private int Ping, unk5, Latency, Round;
        public BATTLE_TIMERSYNC_REC(GameClient client, byte[] data)
        {
            makeme(client, data);
        }

        public override void read()
        {
            TimeRemaining = readUD();
            unk0 = readT(); //BanMotive1
            Round = readC(); //Round da partida (Não o round atual do jogador)
            Ping = readC(); //ping   
            unk5 = readC(); //BanMotive2
            Latency = readH(); //Latência?
        }

        public override void run()
        {
            try
            {
                Account p = _client._player;
                if (p == null)
                    return;
                Room room = p._room;
                if (room == null)
                    return;
                bool isBotMode = room.isBotMode();
                SLOT slot = room.getSlot(p._slotId);
                if (slot == null || slot.state != SLOT_STATE.BATTLE)
                    return;

                if (unk0 != 1 || unk5 != 0)
                {
                    bool dc = false;
                    bool autoBan = false;
                    string msg = "[ANT HACK] O jogador " + p.player_name + " foi automaticamente BANIDO!";

                    switch (unk5)
                    {
                        case 1:
                            autoBan = true;
                            dc = true;
                            break;
                        case 24:
                            dc = true;
                            break;

                    }
                    SaveLog.LogAbuse((HackType)unk5 + " [" + unk0 + "," + unk5 + "]  Player: " + p.player_name + " pId:" + p.player_id);
                    if (autoBan)
                    {
                        ComDiv.updateDB("contas", "access_level", -1, "player_id", p.player_id);
                        p.access = AccessLevel.Banned;
                        using (SERVER_MESSAGE_ANNOUNCE_PAK packet = new SERVER_MESSAGE_ANNOUNCE_PAK(msg))
                            room.SendPacketToPlayers(packet);
                        SaveLog.info(msg);
                        Printf.info(msg);
                    }
                    if (dc)
                    {
                        p.SendPacket(new SERVER_MESSAGE_DISCONNECT_PAK());
                        Printf.info("Jogador " + p.player_name + " desconectado por abuso");
                        SaveLog.info("Jogador " + p.player_name + " desconectado por abuso");
                    }
                        
                }
                    
                room._timeRoom = TimeRemaining;
                SyncPlayerPings(p, room, slot, isBotMode);//ENVIA A INFO PRO HOST
                if ((TimeRemaining > 0x80000000)&& !room.swapRound && CompareRounds(room, Round) && (int)room._state == 5)
                    EndRound(room, isBotMode);
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[BATTLE_TIMERSYNC_REC.run] Erro fatal!");
            }
        }
        private void SyncPlayerPings(Account p, Room room, SLOT slot, bool isBotMode)
        {
            /*
            if (p.DebugPing && (DateTime.Now - p.LastPingDebug).TotalSeconds >= 5)
            {
                p.LastPingDebug = DateTime.Now;
                p.SendPacket(new AUTH_RECV_WHISPER_PAK("Latency", Latency + "ms (" + Ping + " bar)", true));
            }
            */
            

            if (isBotMode) return;
            slot.latency = Latency;
            slot.ping = Ping;

            if (slot.latency >= ConfigGS.maxBattleLatency)
            {
                slot.failLatencyTimes++;
            }
            else {
                slot.failLatencyTimes = 0;
            }  

            if (slot.failLatencyTimes < ConfigGS.maxRepeatLatency)
            {
                if ((double)(DateTime.Now - room.LastPingSync).TotalSeconds < 7)
                {
                    return;
                }
                if (Latency < 5000 && Ping > 1) // Ping Fake
                {
                    slot.ping = 5; // 5 barras
                }
                byte[] pings = new byte[16];
                for (int i = 0; i < 16; i++)
                {
                    pings[i] = (byte)room._slots[i].ping;
                }
                using (BATTLE_SENDPING_PAK packet = new BATTLE_SENDPING_PAK(pings))
                    room.SendPacketToPlayers(packet, SLOT_STATE.BATTLE, 0);
                room.LastPingSync = DateTime.Now;

            }
            else
            {
                SaveLog.info("Player desconectado devido a latencia");
                SaveLog.warning("Player '" + p.player_name + "' (Id: " + slot._playerId + ") desconectado devido a latencia (" + slot.latency + "/" + ConfigGS.maxBattleLatency + "ms)");
                _client.Close(500);
                return;
            }
        }
        private bool CompareRounds(Room room, int externValue)
        {
            if (room.room_type == (int)RoomType.Boss || room.room_type == (int)RoomType.Cross_Counter)
                return (room.rodada == externValue);
            else
                return (room.rodada == externValue + 1);
        }
        private void EndRound(Room room, bool isBotMode)
        {
            try
            {
                room.swapRound = true;
                if (room.room_type == 7 || room.room_type == 12)
                {
                    if (room.rodada == 1)
                    {
                        room.rodada = 2;
                        for (int i = 0; i < 16; i++)
                        {
                            SLOT slot = room._slots[i];
                            if (slot.state == SLOT_STATE.BATTLE)
                            {
                                slot.killsOnLife = 0;
                                slot.lastKillState = 0;
                                slot.repeatLastState = false;
                            }
                        }
                        List<int> dinos = AllUtils.getDinossaurs(room, true, -2);
                        using (BATTLE_ROUND_WINNER_PAK packet = new BATTLE_ROUND_WINNER_PAK(room, 2, RoundEndType.TimeOut))
                        using (BATTLE_ROUND_RESTART_PAK packet2 = new BATTLE_ROUND_RESTART_PAK(room, dinos, isBotMode))
                            room.SendPacketToPlayers(packet, packet2, SLOT_STATE.BATTLE, 0);

                        room.round.StartJob(5250, (callbackState) =>
                        {
                            if (room._state == RoomState.Battle)
                            {
                                room.BattleStart = DateTime.Now.AddSeconds(5);
                                using (BATTLE_TIMERSYNC_PAK packet = new BATTLE_TIMERSYNC_PAK(room))
                                    room.SendPacketToPlayers(packet, SLOT_STATE.BATTLE, 0);
                            }
                            room.swapRound = false;
                            lock (callbackState)
                            { room.round.Timer = null; }
                        });
                    }
                    else if (room.rodada == 2)
                        AllUtils.EndBattle(room, isBotMode);
                }
                else if (room.thisModeHaveRounds())
                {
                    int winner = 1;
                    if (room.room_type != 3)
                        room.blue_rounds++;
                    else
                    {
                        if (room.Bar1 > room.Bar2)
                        {
                            room.red_rounds++;
                            winner = 0;
                        }
                        else if (room.Bar1 < room.Bar2)
                            room.blue_rounds++;
                        else
                            winner = 2;
                    }
                    AllUtils.BattleEndRound(room, winner, RoundEndType.TimeOut);
                }
                else
                {
                    List<Account> players = room.getAllPlayers(SLOT_STATE.READY, 1);
                    if (players.Count == 0)
                        goto EndLabel;
                    TeamResultType winnerTeam = AllUtils.GetWinnerTeam(room);
                    room.CalculateResult(winnerTeam, isBotMode);
                    using (BATTLE_ROUND_WINNER_PAK packet = new BATTLE_ROUND_WINNER_PAK(room, winnerTeam, RoundEndType.TimeOut))
                    {
                        ushort inBattle, missionCompletes;
                        byte[] a1;
                        AllUtils.getBattleResult(room, out missionCompletes, out inBattle, out a1);
                        byte[] data = packet.GetCompleteBytes();
                        foreach (Account pR in players)
                        {
                            if (room._slots[pR._slotId].state == SLOT_STATE.BATTLE)
                                pR.SendCompletePacket(data);
                            pR.SendPacket(new BATTLE_ENDBATTLE_PAK(pR, winnerTeam, inBattle, missionCompletes, isBotMode, a1));
                        }
                    }
                EndLabel:
                    AllUtils.resetBattleInfo(room);
                }
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[BATTLE_TIMERSYNC_REC2.EndRound] Erro fatal!");
                if (room != null)
                    SaveLog.fatal("[!] Crash no BATTLE_TIMERSYNC_REC, Sala: " + room._roomId + ";" + room._channelId + ";" + room.room_type);
            }
        }
    }
}