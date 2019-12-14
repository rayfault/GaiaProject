using GaiaCore.Gaia.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GaiaCore.Gaia
{
    public class Hive : Faction
    {
        public Hive(GaiaGame gg) : base(FactionName.Hive, gg)
        {
            this.KoreanName = "하이브";
            this.ChineseName = "蜂人";
            base.SetColor(1);
            AllianceList = new List<Tuple<int, int>>();
        }

        public List<Tuple<int, int>> AllianceList { set; get; }
        public override Terrain OGTerrain { get => Terrain.Red; }

        protected override int CalQICIncome()
        {
            return base.CalQICIncome() + 1;
        }
        public override bool BuildMine(Map map, int row, int col, out string log)
        {
            if (FactionSpecialAbility > 0)
            {
                return ExcuteSHAbility(new Tuple<int, int>(row, col), out log);
            }
            return base.BuildMine(map, row, col, out log);
        }
        public override bool BuildIntialMine(Map map, int row, int col, out string log)
        {
            log = string.Empty;
            if (!(map.HexArray[row, col].OGTerrain == OGTerrain))
            {
                log = "하이브 지형이 아닙니다.";
                return false;
            }
            if (!(map.HexArray[row, col].Building == null && map.HexArray[row, col].FactionBelongTo == null))
            {
                log = "이미 건물이 지어져 있습니다.";
                return false;
            }

            map.HexArray[row, col].Building = StrongHold;
            map.HexArray[row, col].FactionBelongTo = FactionName;
            StrongHold = null;
            AllianceList.Add(new Tuple<int, int>(row, col));
            CallSpecialSHBuild();
            return true;
        }

        public override bool FinishIntialMines()
        {
            return StrongHold == null;
        }

        public override bool ForgingAllianceCheck(List<Tuple<int, int>> list, out string log)
        {
            log = string.Empty;
            var map = GaiaGame.Map;

            if (list != null)
            {
                int oldAllianceCount;
                var allianceListClone = new List<Tuple<int, int>>(AllianceList);

                if (list.Count == 1 && AllianceList.Count == 1 && list.Exists(x =>
                {
                    TerrenHex terrenHex = GaiaGame.Map.HexArray[x.Item1, x.Item2];
                    return (terrenHex.Building != null && terrenHex.FactionBelongTo == FactionName);
                }))
                {
                    allianceListClone.Clear();
                    allianceListClone.AddRange(list);
                    do
                    {
                        oldAllianceCount = allianceListClone.Count;
                        var hexlist = GaiaGame.Map.GetHexListForBuildingAndSatellite(FactionName, list);
                        var newNeighboor = hexlist.Where(x => !allianceListClone.Contains(x)).ToList().FindAll(x => allianceListClone.Exists(y => GaiaGame.Map.CalTwoHexDistance(x.Item1, x.Item2, y.Item1, y.Item2) == 1));
                        allianceListClone.AddRange(newNeighboor);
                    }
                    while (oldAllianceCount != allianceListClone.Count);
                    
                    if (allianceListClone.Sum(x => (map.GetHex(x).Building?.MagicLevel).GetValueOrDefault() + (map.GetHex(x).IsSpecialSatelliteForHive ? 1 : 0)) >= 7)
                    {
                        AllianceList.ForEach(x =>
                        {
                            GaiaGame.Map.HexArray[x.Item1, x.Item2].IsAlliance = false;
                        });

                        AllianceList.Clear();
                        AllianceList.AddRange(list);
                        return true;
                    }

                    log = "연방 파워가 부족해서 초기 위치를 바꿀 수 없습니다.";
                    return false;
                }

                var SatelliteHexList = list.Where(x => GaiaGame.Map.HexArray[x.Item1, x.Item2].TFTerrain == Terrain.Empty && !GaiaGame.Map.HexArray[x.Item1, x.Item2].Satellite.Contains(FactionName))
                        .ToList();
                if (SatelliteHexList.Count != list.Count)
                {
                    log = "위성 위치만 설정해주세요. 필요 없다면 바로 연방만 선택하시면 됩니다.";
                    return false;
                }
                if (QICs < SatelliteHexList.Count)
                {
                    log = string.Format("QIC 갯수가{0}, 위성갯수{1} 보다 부족합니다.", QICs, SatelliteHexList.Count);
                    return false;
                }

                if (list.Exists(x =>
                {
                    TerrenHex terrenHex = GaiaGame.Map.HexArray[x.Item1, x.Item2];
                    return terrenHex.Satellite.Contains(FactionName);
                }))
                {
                    log = "이미 위성이 있는 자리를 재설정했습니다.";
                    return false;
                }
                //allianceListClone.AddRange(list);
                do
                {
                    oldAllianceCount = allianceListClone.Count;
                    var hexlist = map.GetHexListForBuildingAndSatellite(FactionName, list);
                    var newNeighboor = hexlist.Where(x => !allianceListClone.Contains(x)).ToList().FindAll(x => allianceListClone.Exists(y => map.CalTwoHexDistance(x.Item1, x.Item2, y.Item1, y.Item2) == 1));
                    allianceListClone.AddRange(newNeighboor);
                }
                while (oldAllianceCount != allianceListClone.Count);
                var altNum = GameTileList.Count(x => x is AllianceTile) - (m_TransformLevel == 5 ? 1 : 0) + 1;
                if (allianceListClone.Sum(x => (map.GetHex(x).Building?.MagicLevel).GetValueOrDefault() + (map.GetHex(x).IsSpecialSatelliteForHive ? 1 : 0)) < m_allianceMagicLevel * altNum)
                {
                    log = string.Format("현재 건물 파워 수치가{0}, 필요 수치{1}보다 작습니다.", allianceListClone.Sum(x => map.GetHex(x).Building?.MagicLevel), m_allianceMagicLevel * altNum);
                    return false;
                }
            }
            else
            {
                var altNum = GameTileList.Count(x => x is AllianceTile) - (m_TransformLevel == 5 ? 1 : 0) + 1;
                if (GetMainAllianceGrade() < m_allianceMagicLevel * altNum)
                {
                    log = string.Format("현재 건물 파워 수치가{0}, 필요 수치{1}보다 작습니다.", GetMainAllianceGrade(), m_allianceMagicLevel * altNum);
                    return false;
                }
            }
            return true;
        }

        public override void ForgingAllianceGetTiles(List<Tuple<int, int>> list)
        {
            if (list == null)
            {
                list = new List<Tuple<int, int>>();
            }
            int oldAllianceCount;
            var allianceListClone = new List<Tuple<int, int>>(AllianceList);
            //allianceListClone.AddRange(list);
            do
            {
                oldAllianceCount = allianceListClone.Count;
                var hexlist = GaiaGame.Map.GetHexListForBuildingAndSatellite(FactionName, list);
                var newNeighboor = hexlist.Where(x => !allianceListClone.Contains(x)).ToList().FindAll(x => allianceListClone.Exists(y => GaiaGame.Map.CalTwoHexDistance(x.Item1, x.Item2, y.Item1, y.Item2) == 1));
                allianceListClone.AddRange(newNeighboor);
            }
            while (oldAllianceCount != allianceListClone.Count);

            list.AddRange(allianceListClone.Where(x => GaiaGame.Map.GetHex(x).TFTerrain != Terrain.Empty && GaiaGame.Map.GetHex(x).IsAlliance == false).ToList());
            base.ForgingAllianceGetTiles(list);
        }

        public int GetMainAllianceGrade()
        {
            int oldAllianceCount;
            do
            {
                oldAllianceCount = AllianceList.Count;
                var hexlist = GaiaGame.Map.GetHexListForBuildingAndSatellite(FactionName);
                var newNeighboor = hexlist.Where(x => !AllianceList.Contains(x)).ToList().FindAll(x => AllianceList.Exists(y => GaiaGame.Map.CalTwoHexDistance(x.Item1, x.Item2, y.Item1, y.Item2) == 1));
                AllianceList.AddRange(newNeighboor);
            }
            while (oldAllianceCount != AllianceList.Count);
            AllianceList.Where(x => GaiaGame.Map.GetHex(x).TFTerrain != Terrain.Empty && GaiaGame.Map.GetHex(x).IsAlliance == false).ToList().ForEach(x => GaiaGame.Map.GetHex(x).IsAlliance = true);
            return AllianceList.Sum(x => (GaiaGame.Map.GetHex(x).Building?.MagicLevel).GetValueOrDefault() + (GaiaGame.Map.GetHex(x).IsSpecialSatelliteForHive ? 1 : 0));
        }
        protected override void CallSpecialSHBuild()
        {
            AddGameTiles(new Hiv());
            base.CallSpecialSHBuild();
        }

        public bool ExcuteSHAbility(Tuple<int, int> pos, out string log)
        {
            log = string.Empty;
            var map = GaiaGame.Map;
            if (map.GetHex(pos) == null)
            {
                log = "정상적인 공간이 아닙니다.";
                return false;
            }
            if (map.GetHex(pos).TFTerrain != Terrain.Empty)
            {
                log = "행성 위에는 놓을 수 없습니다.";
                return false;
            }
            if (map.GetHex(pos).Satellite.Contains(FactionName))
            {
                log = "위성 위에는 놓을 수 없습니다.";
                return false;
            }
            var distanceNeed = GaiaGame.Map.CalShipDistanceNeed(pos.Item1, pos.Item2, FactionName);
            var qicship = Math.Max((distanceNeed - GetShipDistance + 1) / 2, 0);
            if (QICs * 2 < distanceNeed - GetShipDistance)
            {
                log = string.Format("해당 지역에 짓기 위해서는, {0}개의 정보토큰이 필요합니다.", Math.Max((distanceNeed - GetShipDistance + 1) / 2, 0));
                return false;
            }
            ActionQueue.Enqueue(() =>
            {
                map.GetHex(pos).AddSatellite(FactionName);
                map.GetHex(pos).IsSpecialSatelliteForHive = true;
                QICs -= qicship;
            });
            FactionSpecialAbility--;
            return true;
        }
        public class Hiv : MapAction
        {
            public override string desc => "SH능력";
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
