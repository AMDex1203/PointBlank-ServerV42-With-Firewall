﻿using Core.Logs;
using Core.managers;
using Core.models.account.players;
using Core.models.enums;
using Core.server;
using Game.data.model;
using Game.data.utils;
using Game.global.serverpacket;
using System;

namespace Game.global.clientpacket
{
    public class INVENTORY_LEAVE_REC : ReceiveGamePacket
    {
        private int erro, type;
        private PlayerEquipedItems data;
        public INVENTORY_LEAVE_REC(GameClient client, byte[] data)
        {
            makeme(client, data);
        }

        public override void read()
        {
            type = readD();
        }

        public override void run()
        {
            try
            {
                if (_client == null)
                    return;
                Account p = _client._player;
                if (p == null)
                    return;
                data = new PlayerEquipedItems();
                DBQuery query = new DBQuery();
                if ((type & 1) == 1)
                    LoadCharaData(p, query);
                if ((type & 2) == 2)
                    LoadWeaponsData(p, query);
                if (ComDiv.updateDB("contas", "player_id", p.player_id, query.GetTables(), query.GetValues()))
                {
                    UpdateChara(p);
                    UpdateWeapons(p);
                }
                query = null;
                Room room = p._room;
                if (room != null)
                {
                    if (type > 0)
                        AllUtils.updateSlotEquips(p, room);
                    room.changeSlotState(p._slotId, SLOT_STATE.NORMAL, true);
                }
                _client.SendPacket(new INVENTORY_LEAVE_PAK(erro, (erro > 0 ? 3 : 0)));
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[INVENTORY_LEAVE_REC.run] Erro fatal!");
            }
        }
        private void LoadWeaponsData(Account p, DBQuery query)
        {
            data._primary = readD();
            data._secondary = readD();
            data._melee = readD();
            data._grenade = readD();
            data._special = readD();
            if (p != null)
                PlayerManager.updateWeapons(data, p._equip, query);
            else erro = -1;
        }
        private void UpdateWeapons(Account p)
        {
            if ((type & 2) == 2)
            {
                p._equip._primary = data._primary;
                p._equip._secondary = data._secondary;
                p._equip._melee = data._melee;
                p._equip._grenade = data._grenade;
                p._equip._special = data._special;
            }
        }
        private void UpdateChara(Account p)
        {
            if ((type & 1) == 1)
            {
                p._equip._red = data._red;
                p._equip._blue = data._blue;
                p._equip._helmet = data._helmet;
                p._equip._beret = data._beret;
                p._equip._dino = data._dino;
            }
        }
        private void LoadCharaData(Account p, DBQuery query)
        {
            data._red = readD();
            data._blue = readD();
            data._helmet = readD();
            data._beret = readD();
            data._dino = readD();
            if (p != null)
                PlayerManager.updateChars(data, p._equip, query);
            else erro = -1;
        }
    }
}