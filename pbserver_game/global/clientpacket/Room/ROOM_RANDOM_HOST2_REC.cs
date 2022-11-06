﻿using Core.Logs;
using Core.models.enums;
using Core.models.room;
using Game.data.model;
using Game.global.serverpacket;
using System;
using System.Collections.Generic;

namespace Game.global.clientpacket
{
    internal class ROOM_RANDOM_HOST2_REC : ReceiveGamePacket
    {
        private List<SLOT> slots = new List<SLOT>();
        public ROOM_RANDOM_HOST2_REC(GameClient client, byte[] data)
        {
            makeme(client, data);
        }

        public override void read()
        {
        }

        public override void run()
        {
            try
            {
                Account p = _client._player;
                Room room = p == null ? null : p._room;
                if (room != null && room._leader == p._slotId && room._state == RoomState.Ready)
                {
                    lock (room._slots)
                        for (int i = 0; i < 16; i++)
                        {
                            SLOT slot = room._slots[i];
                            if (slot._playerId > 0 && i != room._leader)
                                slots.Add(slot);
                        }
                    if (slots.Count > 0)
                    {
                        SLOT slot = slots[new Random().Next(slots.Count)];
                        Account player = room.getPlayerBySlot(slot);
                        if (player != null)
                        {
                            room.setNewLeader(slot._id, 0, room._leader, false);
                            using (ROOM_RANDOM_HOST_PAK packet = new ROOM_RANDOM_HOST_PAK(slot._id))
                                room.SendPacketToPlayers(packet);
                            room.updateSlotsInfo();
                        }
                        else
                            _client.SendPacket(new ROOM_RANDOM_HOST_PAK(0x80000000));
                        slots = null;
                    }
                    else
                        _client.SendPacket(new ROOM_RANDOM_HOST_PAK(0x80000000));
                }
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[ROOM_RANDOM_HOST2_REC.run] Erro fatal!");
            }
        }
    }
}