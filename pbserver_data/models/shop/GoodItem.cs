﻿using Core.models.account.players;

namespace Core.models.shop
{
    public class GoodItem
    {
        public int price_gold, price_cash, auth_type, buy_type2, buy_type3, id, tag, title, visibility;
        public ItemsModel _item = new ItemsModel { _equip = 1 };
    }
}