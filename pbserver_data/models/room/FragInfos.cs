﻿using Core.models.enums.room;
using System.Collections.Generic;

namespace Core.models.room
{
    public class FragInfos
    {
        public byte killerIdx, killsCount, flag;
        public CharaKillType killingType;
        public int weapon, Score;
        public float x, y, z;
        public List<Frag> frags = new List<Frag>();
        public KillingMessage GetAllKillFlags()
        {
            KillingMessage km = 0;
            for (int i = 0; i < frags.Count; i++)
            {
                Frag frag = frags[i];
                if (!km.HasFlag(frag.killFlag))
                    km |= frag.killFlag;
            }
            return km;
        }
    }
}