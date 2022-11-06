﻿using Auth.data.model;
using Core.Logs;
using Core.models.account;
using Core.models.account.players;
using Core.models.enums.flags;
using Core.sql;
using Npgsql;
using System;
using System.Collections.Generic;

namespace Auth.data.managers
{
    public class AccountManager
    {
        public SortedList<long, Account> _contas = new SortedList<long, Account>();
        private static AccountManager acm = new AccountManager();
        public bool AddAccount(Account acc)
        {
            lock (_contas)
            {
                if (!_contas.ContainsKey(acc.player_id))
                {
                    _contas.Add(acc.player_id, acc);
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Pesquisa uma conta na Database.
        /// </summary>
        /// <param name="valor">Parâmetro de pesquisa 1</param>
        /// <param name="valor2">Parâmetro de pesquisa 2</param>
        /// <param name="type">Tipo de pesquisa. 0 = Login; 1 = Id; 2 = Login e Senha</param>
        /// <param name="searchFlag">Detalhes (DB;Flag)\n0 = Nada\n1 = Títulos\n2 = Bônus\n4 = Amigos\n8 = Eventos\n16 = Configurações</param>
        /// <returns></returns>
        public Account getAccountDB(object valor, object valor2, int type, int searchFlag)
        {
            if (type == 0 && (string)valor == "" || type == 1 && (long)valor == 0 || type == 2 && (string.IsNullOrEmpty((string)valor) || string.IsNullOrEmpty((string)valor2)))
                return null;
            Account conta = null;
            try
            {
                using (NpgsqlConnection connection = SQLjec.getInstance().conn())
                {
                    NpgsqlCommand command = connection.CreateCommand();
                    connection.Open();
                    command.Parameters.AddWithValue("@valor", valor);
                    if (type == 0)
                        command.CommandText = "SELECT * FROM contas WHERE login=@valor LIMIT 1";
                    else if (type == 1)
                        command.CommandText = "SELECT * FROM contas WHERE player_id=@valor LIMIT 1";
                    else if (type == 2)
                    {
                        command.Parameters.AddWithValue("@valor2", valor2);
                        command.CommandText = "SELECT * FROM contas WHERE login=@valor AND password=@valor2 LIMIT 1";
                    }
                    NpgsqlDataReader data = command.ExecuteReader();
                    while (data.Read())
                    {
                        conta = new Account();
                        conta.login = data.GetString(0);
                        conta.password = data.GetString(1);
                        conta.SetPlayerId(data.GetInt64(2), searchFlag);
                        conta.player_name = data.GetString(3);
                        conta.name_color = data.GetInt16(4);
                        conta.clan_id = data.GetInt32(5);
                        conta._rank = data.GetInt16(6);
                        conta._gp = data.GetInt32(7);
                        conta._exp = data.GetInt32(8);
                        conta.pc_cafe = data.GetInt32(9);
                        conta._statistic.fights = data.GetInt32(10);
                        conta._statistic.fights_win = data.GetInt32(11);
                        conta._statistic.fights_lost = data.GetInt32(12);
                        conta._statistic.kills_count = data.GetInt32(13);
                        conta._statistic.deaths_count = data.GetInt32(14);
                        conta._statistic.headshots_count = data.GetInt32(15);
                        conta._statistic.escapes = data.GetInt32(16);
                        conta.access = data.GetInt16(17);
                        conta._money = data.GetInt32(20);
                        conta._isOnline = data.GetBoolean(21);
                        conta._equip._primary = data.GetInt32(22);
                        conta._equip._secondary = data.GetInt32(23);
                        conta._equip._melee = data.GetInt32(24);
                        conta._equip._grenade = data.GetInt32(25);
                        conta._equip._special = data.GetInt32(26);
                        conta._equip._red = data.GetInt32(27);
                        conta._equip._blue = data.GetInt32(28);
                        conta._equip._helmet = data.GetInt32(29);
                        conta._equip._dino = data.GetInt32(30);
                        conta._equip._beret = data.GetInt32(31);
                        conta.brooch = data.GetInt32(32);
                        conta.insignia = data.GetInt32(33);
                        conta.medal = data.GetInt32(34);
                        conta.blue_order = data.GetInt32(35);
                        conta._mission.mission1 = data.GetInt32(36);
                        conta.clanAccess = data.GetInt16(37);
                        conta.effects = (CupomEffects)data.GetInt64(39);
                        conta._statistic.fights_draw = data.GetInt32(40);
                        conta._mission.mission2 = data.GetInt32(41);
                        conta._mission.mission3 = data.GetInt32(42);
                        conta._statistic.totalkills_count = data.GetInt32(43);
                        conta._statistic.totalfights_count = data.GetInt32(44);
                        conta._status.SetData((uint)data.GetInt64(45), conta.player_id);

                        if (AddAccount(conta) && conta._isOnline)
                            conta.setOnlineStatus(false);
                    }
                    command.Dispose();
                    data.Dispose();
                    data.Close();
                    connection.Dispose();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[AccountManager.getAccountDB] Erro fatal!");
            }
            return conta;
        }
        /// <summary>
        /// Pesquisa as contas dos amigos na Database e retorna com as informações básicas.
        /// Retorna:
        /// Apelido, Id do jogador, Patente, "online" e "status".
        /// </summary>
        /// <param name="playerId"></param>
        /// <returns></returns>
        public void getFriendlyAccounts(FriendSystem system)
        {
            if (system == null || system._friends.Count == 0)
                return;
            try
            {
                using (NpgsqlConnection connection = SQLjec.getInstance().conn())
                {
                    NpgsqlCommand command = connection.CreateCommand();
                    connection.Open();
                    string loaded = "";
                    List<string> parameters = new List<string>();
                    for (int idx = 0; idx < system._friends.Count; idx++)
                    {
                        Friend friend = system._friends[idx];
                        string param = "@valor" + idx;
                        command.Parameters.AddWithValue(param, friend.player_id);
                        parameters.Add(param);
                    }
                    loaded = string.Join(",", parameters.ToArray());
                    command.CommandText = "SELECT player_name,player_id,rank,online,status FROM contas WHERE player_id in (" + loaded + ") ORDER BY player_id";
                    NpgsqlDataReader data = command.ExecuteReader();
                    while (data.Read())
                    {
                        Friend friend = system.GetFriend(data.GetInt64(1));
                        if (friend != null)
                        {
                            friend.player.player_name = data.GetString(0);
                            friend.player._rank = data.GetInt32(2);
                            friend.player._isOnline = data.GetBoolean(3);
                            friend.player._status.SetData((uint)data.GetInt64(4), friend.player_id);
                            if (friend.player._isOnline && !_contas.ContainsKey(friend.player_id))
                            {
                                friend.player.setOnlineStatus(false);
                                friend.player._status.ResetData(friend.player_id);
                            }
                        }
                    }
                    command.Dispose();
                    data.Dispose();
                    data.Close();
                    connection.Dispose();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[AccountManager.FriendlyAccounts] Erro fatal!");
            }
        }
        public void getFriendlyAccounts(FriendSystem system, bool isOnline)
        {
            if (system == null || system._friends.Count == 0)
                return;
            try
            {
                using (NpgsqlConnection connection = SQLjec.getInstance().conn())
                {
                    NpgsqlCommand command = connection.CreateCommand();
                    string loaded = "";
                    List<string> parameters = new List<string>();
                    for (int idx = 0; idx < system._friends.Count; idx++)
                    {
                        Friend friend = system._friends[idx];
                        if (friend.state > 0)
                            return;
                        string param = "@valor" + idx;
                        command.Parameters.AddWithValue(param, friend.player_id);
                        parameters.Add(param);
                    }
                    loaded = string.Join(",", parameters.ToArray());
                    if (loaded == "")
                        return;
                    connection.Open();
                    command.Parameters.AddWithValue("@on", isOnline);
                    command.CommandText = "SELECT player_name,player_id,rank,status FROM contas WHERE player_id in (" + loaded + ") AND online=@on ORDER BY player_id";
                    NpgsqlDataReader data = command.ExecuteReader();
                    while (data.Read())
                    {
                        Friend friend = system.GetFriend(data.GetInt64(1));
                        if (friend != null)
                        {
                            friend.player.player_name = data.GetString(0);
                            friend.player._rank = data.GetInt32(2);
                            friend.player._isOnline = isOnline;
                            friend.player._status.SetData((uint)data.GetInt64(3), friend.player_id);
                            if (isOnline && !_contas.ContainsKey(friend.player_id))
                            {
                                friend.player.setOnlineStatus(false);
                                friend.player._status.ResetData(friend.player_id);
                            }
                        }
                    }
                    command.Dispose();
                    data.Dispose();
                    data.Close();
                    connection.Dispose();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                SaveLog.fatal(ex.ToString());
                Printf.b_danger("[AccountManager.FriendAccounts2] Erro fatal!");
            }
        }
        static AccountManager()
        {
        }
        public static AccountManager getInstance()
        {
            return acm;
        }
        /// <summary>
        /// Procura no cache uma conta pelo Id. Caso não encontrada, é feita uma procura na Database.
        /// </summary>
        /// <param name="id">Id da conta</param>
        /// <returns></returns>
        public Account getAccount(long id)
        {
            if (id == 0)
                return null;
            try
            {
                Account p = null;
                return _contas.TryGetValue(id, out p) ? p : getAccountDB(id, null, 1, 0);
            }
            catch { return null; }
        }
        /// <summary>
        /// Procura no cache uma conta pelo Id. É possível escolher se, caso não encontrada, procurar a conta na Database.
        /// </summary>
        /// <param name="id">Id da conta</param>
        /// <param name="noUseDB">Não usar Database</param>
        /// <returns></returns>
        public Account getAccount(long id, bool noUseDB)
        {
            if (id == 0)
                return null;
            try
            {
                Account p = null;
                return _contas.TryGetValue(id, out p) ? p : (noUseDB ? null : getAccountDB(id, null, 1, 0));
            }
            catch { return null; }
        }
        
    }
}