﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GaiaCore.Gaia
{
    public class MadAndroid : Faction
    {
        public MadAndroid(GaiaGame gg) :base(FactionName.MadAndroid, gg)
        {
            this.ChineseName = "疯狂机器";
            this.ColorCode = colorList[5];
            this.ColorMap = colorMapList[5];

        }
        public override Terrain OGTerrain { get => Terrain.Gray; }

        protected override int CalKnowledgeIncome()
        {
            var ret = 0;
            ret += m_TradeCenterCount - TradeCenters.Count;
            if (Academy1 == null)
            {
                ret += CallAC1Income();
            }

            ret += GameTileList.Sum(x => x.GetKnowledgeIncome());

            switch (ScienceLevel)
            {
                case 1:
                    ret += 1;
                    break;
                case 2:
                    ret += 2;
                    break;
                case 3:
                    ret += 3;
                    break;
                case 4:
                    ret += 4;
                    break;
            }
            return ret;
        }

        protected override int CalCreditIncome()
        {
            int ret = 0;
            if (ResearchLabs.Count == 2)
            {
                ret += 3;
            }
            else if (ResearchLabs.Count == 1)
            {
                ret += 7;
            }
            else if (ResearchLabs.Count == 0)
            {
                ret += 12;
            }
            ret += GameTileList.Sum(x => x.GetCreditIncome());
            switch (EconomicLevel)
            {
                case 1:
                    ret += 2;
                    break;
                case 2:
                    ret += 2;
                    break;
                case 3:
                    ret += 3;
                    break;
                case 4:
                    ret += 4;
                    break;
                case 5:
                    ret += 6;
                    break;
                default:
                    break;
            }
            return ret;
        }
    }
}
