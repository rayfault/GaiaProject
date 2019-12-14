using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GaiaCore.Gaia
{
    public class BalTak : Faction
    {
        public BalTak(GaiaGame gg) : base(FactionName.BalTak, gg)
        {
            this.KoreanName = "발타크";
            this.ChineseName = "炽炎族";
            base.SetColor(2);
            if (gg != null)
            {
                IncreaseTech("gaia");
            }
            QICs -= 1;
            m_powerToken2 -= 2;
            TempGaias = new List<GaiaBuilding>();
            GaiasGaiaArea = new List<GaiaBuilding>();
        }


        public override Terrain OGTerrain { get => Terrain.Orange; }
        public List<GaiaBuilding> TempGaias { set; get; }
        public List<GaiaBuilding> GaiasGaiaArea { set; get; }

        public override bool IsIncreaseTechLevelByIndexValidate(int index, out string log, bool isIncreaseAllianceTileCost = false)
        {
            log = string.Empty;
            if (StrongHold != null)
            {
                if (index == 1)
                {
                    log = "의회를 짓지 않으면 네비게이션 기술을 올릴 수 없습니다.";
                    return false;
                }
            }
            return base.IsIncreaseTechLevelByIndexValidate(index, out log, isIncreaseAllianceTileCost);
        }

        public override bool ConvertOneResourceToAnother(int rFNum, string rFKind, int rTNum, string rTKind, out string log, int? rTNum2 = default(int?), string rTKind2 = null, int? rFNum2 = null, string rFKind2 = null)
        {
            log = string.Empty;
            var str = rFKind + rTKind;
            switch (str)
            {
                case "gaiaq":
                    if (rFNum != rTNum * 1)
                    {
                        log = "가이아 포머와 1:1 교환해야 합니다.";
                        return false;
                    }
                    if (Gaias.Count < rFNum)
                    {
                        log = "가이아 포머의 갯수가 부족합니다.";
                        return false;
                    }
                    for (int i = 0; i < rFNum; i++)
                    {
                        TempGaias.Add(Gaias.First());
                        Gaias.RemoveAt(0);
                    }
                    TempQICs += rTNum;
                    Action action = () =>
                    {
                        QICs = QICs;
                        GaiasGaiaArea.AddRange(TempGaias);
                        TempGaias.Clear();
                    };
                    ActionQueue.Enqueue(action);
                    return true;
                case "gaiao":
                    if (rFNum != rTNum * 1)
                    {
                        log = "가이아 포머와 1:1 교환해야 합니다.";
                        return false;
                    }
                    if (Gaias.Count < rFNum)
                    {
                        log = "가이아 포머의 갯수가 부족합니다.";
                        return false;
                    }
                    for (int i = 0; i < rFNum; i++)
                    {
                        TempGaias.Add(Gaias.First());
                        Gaias.RemoveAt(0);
                    }
                    TempOre += rTNum;
                    action = () =>
                    {
                        Ore = Ore;
                        GaiasGaiaArea.AddRange(TempGaias);
                        TempGaias.Clear();
                    };
                    ActionQueue.Enqueue(action);
                    return true;
                case "gaiac":
                    if (rFNum != rTNum * 1)
                    {
                        log = "가이아 포머와 1:1 교환해야 합니다.";
                        return false;
                    }
                    if (Gaias.Count < rFNum)
                    {
                        log = "가이아 포머의 갯수가 부족합니다.";
                        return false;
                    }
                    for (int i = 0; i < rFNum; i++)
                    {
                        TempGaias.Add(Gaias.First());
                        Gaias.RemoveAt(0);
                    }
                    TempCredit += rTNum;
                    action = () =>
                    {
                        Credit = Credit;
                        GaiasGaiaArea.AddRange(TempGaias);
                        TempGaias.Clear();
                    };
                    ActionQueue.Enqueue(action);
                    return true;
                case "gaiapwt":
                    if (rFNum != rTNum * 1)
                    {
                        log = "가이아 포머와 1:1 교환해야 합니다.";
                        return false;
                    }
                    if (Gaias.Count < rFNum)
                    {
                        log = "가이아 포머의 갯수가 부족합니다.";
                        return false;
                    }
                    for (int i = 0; i < rFNum; i++)
                    {
                        TempGaias.Add(Gaias.First());
                        Gaias.RemoveAt(0);
                    }
                    TempPowerToken1 += rTNum;
                    action = () =>
                    {
                        PowerToken1 = PowerToken1;
                        GaiasGaiaArea.AddRange(TempGaias);
                        TempGaias.Clear();
                    };
                    ActionQueue.Enqueue(action);
                    return true;
                default:
                    break;
            }
            return base.ConvertOneResourceToAnother(rFNum, rFKind, rTNum, rTKind, out log, rTNum2, rTKind2);
        }

        public override void ResetUnfinishAction()
        {
            Gaias.AddRange(TempGaias);
            TempGaias.Clear();
            base.ResetUnfinishAction();
        }
    }
}
