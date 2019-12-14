using GaiaCore.Gaia.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GaiaCore.Gaia
{
    public class Firaks : Faction
    {
        public Firaks(GaiaGame gg) :base(FactionName.Firaks, gg)
        {
            this.KoreanName = "파이락";
            this.ChineseName = "章鱼人";
            base.SetColor(5);

            m_knowledge -= 1;
            m_ore -= 1;
        }
        public override Terrain OGTerrain { get => Terrain.Gray; }
        public override void CalIncome()
        {
            base.CalIncome();
        }

        protected override int CalKnowledgeIncome()
        {
            return base.CalKnowledgeIncome()+1;
        }

        public bool DowngradeBuilding(int row, int col, out string log)
        {
            log = string.Empty;
            var hex = GaiaGame.Map.HexArray[row, col];
            if (!(hex.FactionBelongTo == this.FactionName && hex.Building is ResearchLab))
            {
                log = "본인 소유의 연구소에 실행하셔야 합니다.";
                return false;
            }
            if (!TradeCenters.Any())
            {
                log = "교역소가 남아있지 않습니다.";
                return false;
            }

            ActionQueue.Enqueue(() =>
            {
                ResearchLabs.Add(hex.Building as ResearchLab);
                hex.Building = TradeCenters.First();
                TradeCenters.RemoveAt(0);
                TriggerRST(typeof(RST2));
                TriggerRST(typeof(RST8));
                //对ATT5(TC>>3VP)计分的支持
                TriggerRST(typeof(ATT5));

                GaiaGame.SetLeechPowerQueue(FactionName, row, col);
            });
            TechTracAdv++;
            FactionSpecialAbility--;
            return true;
        }

        protected override void CallSpecialSHBuild()
        {
            AddGameTiles(new Fir());
            base.CallSpecialSHBuild();
        }

        public class Fir : MapAction
        {
            public override string desc => "SH能力";
            protected override int ResourceCost => 0;
            public override bool CanAction => true;
            public override bool InvokeGameTileAction(Faction faction)
            {
                faction.FactionSpecialAbility++;
                faction.ActionQueue.Enqueue(() =>
                {
                    base.InvokeGameTileAction(faction);
                });
                return true;
            }
        }
    }
}
