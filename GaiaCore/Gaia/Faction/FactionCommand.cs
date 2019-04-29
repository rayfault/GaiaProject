﻿using GaiaCore.Gaia.Tiles;
using GaiaCore.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GaiaCore.Gaia.Game;
using GaiaDbContext.Models.HomeViewModels;
using Remotion.Linq.Utilities;

namespace GaiaCore.Gaia
{
    public abstract partial class Faction
    {
        /// <summary>
        /// 是否执行主要行动
        /// </summary>
        public bool IsActionBurn { get; set; }
        /// <summary>
        /// 黑星数量
        /// </summary>
        public int blankMine { get; set; }

        public virtual bool BuildMine(Map map, int row, int col, out string log)
        {

            log = string.Empty;
            bool isGreenPlanet = false;
            //判断是否是Gaia改变过的绿色星球
            bool isGaiaPlanet = false;
            int transNumNeed = 0;
            if (map.HexArray[row, col] == null)
            {
                log = "정상적인 공간이 아닙니다.";
                return false;
            }
            if (map.HexArray[row, col].TFTerrain == Terrain.Purple)
            {
                log = "불모행성에는 광산을 건설 하실 수 없습니다.";
                return false;
            }
            if (map.HexArray[row, col].TFTerrain == Terrain.Empty)
            {
                log = "우주공간에는 광산을 건설 하실 수 없습니다.";
                return false;
            }

            //矿石铲子
            int oreTF = 0;
            //航海的Q
            int QSHIP = 0;
            if (map.HexArray[row, col].TFTerrain == Terrain.Green)
            {
                isGreenPlanet = true;
                if (map.HexArray[row, col].TFTerrain == Terrain.Green
                    && map.HexArray[row, col].Building is GaiaBuilding
                    && map.HexArray[row, col].FactionBelongTo == FactionName)
                {
                    isGaiaPlanet = true;
                }
                else
                {
                    if (PreBuildGaiaPlanetMine(out log))
                    {
                        return false;
                    }
                }
            }
            else
            {
                transNumNeed = Math.Min(7 - Math.Abs(map.HexArray[row, col].OGTerrain - OGTerrain), Math.Abs(map.HexArray[row, col].OGTerrain - OGTerrain));
                oreTF = Math.Max((transNumNeed - TerraFormNumber), 0);
                if (oreTF * GetTransformCost > Ore)
                {
                    log = string.Format("原始地形为{0},需要地形{1},需要矿石为{2}或者使用action行动获取铲子", map.HexArray[row, col].OGTerrain.ToString(), OGTerrain.ToString(), transNumNeed * GetTransformCost);
                    return false;
                }
            }
            if (Mines.Count < 1)
            {
                log = "已经没有可用的矿场了";
                return false;
            }
            if (!(Credit >= m_MineCreditCost && Ore >= m_MineOreCost + Math.Max((transNumNeed - TerraFormNumber), 0) * GetTransformCost))
            {
                log = "资源不够";
                return false;
            }

            if (!isGaiaPlanet && !(map.HexArray[row, col].Building == null && map.HexArray[row, col].FactionBelongTo == null))
            {
                log = "该地点已经有人占领了";
                return false;
            }
            var distanceNeed = map.CalShipDistanceNeed(row, col, FactionName);

            if (!isGaiaPlanet && QICs * 2 < distanceNeed - GetShipDistance)
            {
                log = string.Format("建筑距离太偏远了,需要{0}个Q来加速", (distanceNeed - GetShipDistance + 1) / 2);
                return false;
            }
            QSHIP = Math.Max((distanceNeed - GetShipDistance + 1) / 2, 0);
            
            //铲子计分，临时铲子和需要的铲子比较，取较大的
            int sjTF = Math.Max(transNumNeed, TerraFormNumber);

            //扣资源建建筑
            Action queue = () =>
            {
                Ore -= m_MineOreCost + oreTF * GetTransformCost;
                Credit -= m_MineCreditCost;
                if (isGaiaPlanet)
                {
                    Gaias.Add(map.HexArray[row, col].Building as GaiaBuilding);
                }
                map.HexArray[row, col].Building = Mines.First();
                map.HexArray[row, col].FactionBelongTo = FactionName;
                Mines.RemoveAt(0);
                if (!isGaiaPlanet && isGreenPlanet)
                {
                    BuildGaiaPlanetMine();
                }
                if (isGreenPlanet)
                {
                    TriggerRST(typeof(RST4));
                    TriggerRST(typeof(RST10));
                    TriggerRST(typeof(STT2));
                    GaiaPlanetNumber++;
                }
                GaiaGame.SetLeechPowerQueue(FactionName, row, col);
                TriggerRST(typeof(RST1));
                TriggerRST(typeof(ATT4));

                for (int i = 0; i < sjTF; i++)
                {
                    TriggerRST(typeof(RST7));
                }
                var surroundhex = map.GetSurroundhexWithBuild(row, col, FactionName);
                if (surroundhex.Exists(x => (map.HexArray[x.Item1, x.Item2].IsAlliance && map.HexArray[x.Item1, x.Item2].FactionBelongTo == FactionName)
                || (this is Lantida && map.HexArray[x.Item1, x.Item2].SpecialBuilding != null && map.HexArray[x.Item1, x.Item2].IsSpecialBuildingAlliance == true)))
                {
                    map.HexArray[row, col].IsAlliance = true;
                }
                if (!isGaiaPlanet)
                {
                    QICs -= QSHIP;
                }
            };
            ActionQueue.Enqueue(queue);
            TerraFormNumber = 0;
            TempShip = 0;
            return true;
        }

        protected virtual void BuildGaiaPlanetMine()
        {
            QICs = QICs;
            TempQICs = 0;
        }

        protected virtual bool PreBuildGaiaPlanetMine(out string log)
        {
            log = string.Empty;
            TempQICs -= 1;
            if (QICs < 0)
            {
                log = "至少需要一块Q";
                return false;
            }
            return false;
        }

        public virtual int GetSatelliteCount()
        {
            var hexList = GaiaGame.Map.GetHexList();
            var q =
                from h in hexList
                where h.Satellite != null && h.Satellite.Contains(FactionName)
                select h;
            return q.Count();
        }

        public virtual int GetAllianceBuilding()
        {
            var hexList = GaiaGame.Map.GetHexList();
            var q =
                from h in hexList
                where h.FactionBelongTo == FactionName && h.IsAlliance == true && !(h.Building is GaiaBuilding)
                select h;
            return q.Count();
        }

        public virtual int GetBuildCount()
        {
            var i1 = Academy1 == null ? 1 : 0;
            var i2 = Academy2 == null ? 1 : 0;
            var i3 = StrongHold == null ? 1 : 0;
            var i4 = ShipLevel == 5 ? 1 : 0;
            return m_MineCount - Mines.Count + m_TradeCenterCount - TradeCenters.Count + m_ReaserchLabCount - ResearchLabs.Count + i1 + i2 + i3 + i4;
        }

        public virtual void PowerUse(int v)
        {
            PowerToken3 -= v;
            PowerToken1 += v;
        }

        protected void TriggerRST(Type type)
        {
            if (GaiaGame.GameStatus.stage <= Stage.SELECTROUNDBOOSTER)
                return;
            if (GaiaGame.RSTList[(GaiaGame.GameStatus.RoundCount - 1)].GetType()==type)
            {
                Score += GaiaGame.RSTList[(GaiaGame.GameStatus.RoundCount - 1)].GetTriggerScore;
            }
            if(GameTileList.Exists(x=>x.GetType()==type))
            {
                
                Score += GameTileList.Find(x => x.GetType() == type).GetTriggerScore;
                //高级版得分统计
                DbTTSave.Score(type,this.GaiaGame,this,true);

            }
        }

        public bool BuildGaia(Map map, int row, int col, out string log)
        {
            log = string.Empty;
            if (m_GaiaLevel == 0)
            {
                log = "执行盖亚行动至少需要一级GaiaLevel";
                return false;
            }
            if (Gaias.Count == 0)
            {
                log = "没有可用的盖亚建筑";
                return false;
            }
            if (PowerToken1 + PowerToken2 + PowerToken3 < GetGaiaCost())
            {
                log = "能量豆资源不够";
                return false;
            }
            if (map.HexArray[row, col].TFTerrain != Terrain.Purple)
            {
                log = "必须为紫星";
                return false;
            }
            if (!(map.HexArray[row, col].Building == null && map.HexArray[row, col].FactionBelongTo == null))
            {
                log = "该地点已经有人占领了";
                return false;
            }
            var distanceNeed = map.CalShipDistanceNeed(row, col, FactionName);

            if (QICs * 2 < distanceNeed - GetShipDistance)
            {
                log = string.Format("建筑距离太偏远了,需要{0}个Q来加速", (distanceNeed - GetShipDistance + 1) / 2);
                return false;
            }

            var QSHIP = Math.Max((distanceNeed - GetShipDistance + 1) / 2, 0);
            //扣资源建建筑
            Action queue = () =>
            {
                RemovePowerToken(GetGaiaCost());
                map.HexArray[row, col].Building = Gaias.First();
                map.HexArray[row, col].FactionBelongTo = FactionName;
                PowerTokenGaia += GetGaiaCost();
                Gaias.RemoveAt(0);
                QICs -= QSHIP;
            };
            ActionQueue.Enqueue(queue);
            TempShip = 0;
            return true;
        }

        public virtual int GetSpaceSectorCount()
        {
            var hexList = GaiaGame.Map.GetHexList();
            var q =
                    from h in hexList
                    where h.FactionBelongTo == FactionName && !(h.Building is GaiaBuilding)
                    group h by h.SpaceSectorName into g
                    select g;
            return q.Count();
        }

        public virtual int GetPlanetTypeCount()
        {
            var hexList = GaiaGame.Map.GetHexList();
            var q =
                from h in hexList
                where h.FactionBelongTo == FactionName && h.TFTerrain != Terrain.Purple
                group h by h.TFTerrain into g
                select g;
            return q.Count();
        }

        public bool IsPlanetTypeExist(Terrain terrain)
        {
            var hexList = GaiaGame.Map.GetHexList();

            return hexList.ToList().Exists(x => x.FactionBelongTo == FactionName && !(x.Building is GaiaBuilding) && x.TFTerrain == terrain);
        }
        public virtual void PowerBurnSpecialPreview(int v)
        {
            return;
        }
        public virtual void PowerBurnSpecialActual(int v)
        {
            return;
        }
        protected virtual void RemovePowerToken(int n)
        {
            if (PowerToken1 + PowerToken2 + PowerToken3 <n)
            {
                throw new Exception(string.Format("没有{0}个能量豆来移除",n));
            }
            PowerToken1 -= n;
            if (PowerToken1 < 0)
            {
                PowerToken2 += PowerToken1;
                PowerToken1 = 0;
            }
            if (PowerToken2 < 0)
            {
                PowerToken3 += PowerToken2;
                PowerToken2 = 0;
            }
        }

        public virtual void GaiaPhaseIncome()
        {
            PowerToken1 += PowerTokenGaia;
            PowerTokenGaia = 0;
            if(this is BalTak)
            {
                var f = (this as BalTak);
                f.Gaias.AddRange(f.GaiasGaiaArea);
                f.GaiasGaiaArea.Clear();
            }
        }

        private int GetGaiaCost()
        {
            if (m_GaiaLevel == 0)
            {
                throw new Exception("0级GaiaLevel不能造Gaia建筑");
            }else if (m_GaiaLevel == 1 || m_GaiaLevel == 2)
            {
                return 6;
            }else if (m_GaiaLevel == 3)
            {
                return 4;
            }else if (m_GaiaLevel == 4 || m_GaiaLevel == 5)
            {
                return 3;
            }
            throw new Exception(m_GaiaLevel+"级GaiaLevel出错");
        }

        public void BuildPowerPreview()
        {
            var pl = new List<int>();
            var ptl = new List<int>();
            CalPowerIncome(pl);
            CalPowerTokenIncome(ptl);
            var pls = new List<Tuple<bool, int>>(); //true为加能量 false为加pwt
            pl.Where(x => x != 0).ToList().ForEach(x => pls.Add(new Tuple<bool, int>(true, x)));
            ptl.Where(x => x != 0).ToList().ForEach(x => pls.Add(new Tuple<bool, int>(false, x)));
            for (int i = 0; i < pls.Count; i++)
            {
                for (int j = i; j < pls.Count; j++)
                {
                    var temp = pls[i];
                    pls[i] = pls[j];
                    pls[j] = temp;
                    var back=BackupResource();
                    pls.ForEach(x =>
                    {
                        if (x.Item1)
                        {
                            PowerIncrease(x.Item2);
                        }
                        else
                        {
                            PowerToken1 += x.Item2;
                        }
                    });
                    if (!PowerPreview.Exists(x => x.Item1 == PowerToken1 && x.Item2 == PowerToken2 && x.Item3 == PowerToken3))
                    {
                        PowerPreview.Add(new Tuple<int, int, int>(PowerToken1, PowerToken2, PowerToken3));
                    }
                    RestoreResource(back);
                }
            }
            if (PowerPreview.Count == 1)
            {
                SetPowerPreview(0);
            }
        }

        public virtual void SetPowerPreview(int i)
        {
            PowerToken1 = PowerPreview[i].Item1;
            PowerToken2 = PowerPreview[i].Item2;
            PowerToken3 = PowerPreview[i].Item3;
            PowerPreview.Clear();
        }

        public virtual void ResetNewRound()
        {
        }

        public int GetFinalEndScore()
        {
            var ret = 0;
            ret += GetTechScoreCount() * 4;
            ret += FinalEndScore;
            ret += GetResouceScore();
            return ret;

        }

        public int GetResouceScore()
        {
            var ret = 0;
            ret += (Ore + PowerToken3 + Credit + Knowledge + QICs + PowerToken2 / 2) / 3;
            return ret;
        }

        public int GetFinalEndScorePreview()
        {
            if (GaiaGame.GameStatus.stage == Stage.GAMEEND)
            {
                return Score;
            }
            else
            {
                if (GaiaGame.FactionNextTurnList.Contains(this))
                {
                    return GetFinalEndScore() + Score;
                }
                else
                {
                    return GetFinalEndScore() + Score+ GameTileList.Sum(y => y.GetTurnEndScore(this));
                }
            }
        }
        public int GetTechScoreCount()
        {
            var ret = 0;
            for (int i = 0; i < 6; i++)
            {
                ret += Math.Max((GetTechLevelbyIndex(i) - 2), 0);
            }
            return ret;
        }

        public virtual bool BuildIntialMine(Map map, int row, int col, out string log)
        {
            log = string.Empty;
            if (map.HexArray[row, col].OGTerrain != OGTerrain)
            {
                log = "地形不符";
                return false;
            }
            if (!(map.HexArray[row, col].Building == null && map.HexArray[row, col].FactionBelongTo == null))
            {
                log = "该地点已经有人占领了";
                return false;
            }

            map.HexArray[row, col].Building = Mines.First();
            map.HexArray[row, col].FactionBelongTo = FactionName;
            Mines.RemoveAt(0);
            return true;
        }

        public bool UpgradeBuilding(Map map, int row, int col, string buildStr, out string log)
        {
            log = string.Empty;
            var hex = map.HexArray[row, col];
            if (hex.FactionBelongTo != FactionName)
            {
                log = string.Format("该地点还不属于{0}", FactionName.ToString());
                return false;
            }
            if (!Enum.TryParse(buildStr, true, out BuildingSyntax syn))
            {
                log = string.Format("没有该建筑物", buildStr);
                return false;
            }
            int oreCost;
            int creditCost;
            Building build;
            Type trigger;
            Type trigger2;
            switch (syn)
            {
                case BuildingSyntax.TC:
                    build = TradeCenters.FirstOrDefault();
                    oreCost = m_TradeCenterOreCost;
                    if (GaiaGame.FactionList.Where(x => x != this).ToList().Exists(y => GaiaGame.Map.GetSurroundhexWithBuild(row, col, y.FactionName,2).Count != 0))
                    {
                        creditCost = m_TradeCenterCreditCostCluster;
                    }
                    else
                    {
                        creditCost = m_TradeCenterCreditCostAlone;
                    }
                    trigger = typeof(RST8);
                    trigger2 = typeof(RST2);
                    break;
                case BuildingSyntax.RL:
                    build = ResearchLabs.FirstOrDefault();
                    oreCost = m_ReaserchLabOreCost;
                    creditCost = m_ReaserchLabCreditCost;
                    trigger = null;
                    trigger2 = null;
                    break;
                case BuildingSyntax.SH:
                    build = StrongHold;
                    oreCost = m_StrongHoldOreCost;
                    creditCost = m_StrongHoldCreditCost;
                    trigger = typeof(RST3);
                    trigger2 = typeof(RST9);
                    if (this is Nevla)
                    {
                        (this as Nevla).IsStrongBuild = true;
                    }
                    break;
                case BuildingSyntax.AC1:
                    build = Academy1;
                    oreCost = m_AcademyOreCost;
                    creditCost = m_AcademyCreditCost;
                    trigger = typeof(RST3);
                    trigger2 = typeof(RST9);
                    break;
                case BuildingSyntax.AC2:
                    build = Academy2;
                    oreCost = m_AcademyOreCost;
                    creditCost = m_AcademyCreditCost;
                    trigger = typeof(RST3);
                    trigger2 = typeof(RST9);
                    if (this is Gleen)
                    {
                        (this as Gleen).IsOreReplaceQICSIncome = false;
                    }
                    break;
                default:
                    log = "未知升级";
                    return false;
            }
            if (build == null)
            {
                log = string.Format("需要该建筑{0}还有剩余", buildStr);
                return false;
            }
            if (hex.Building.GetType() != build.BaseBuilding)
            {
                log = string.Format("需要基础建筑类型{0}", build.BaseBuilding.ToString());
                return false;
            }
            if (hex.TFTerrain==Terrain.Black)
            {
                log = "死星建筑不能升级";
                return false;
            }
            if (Ore < oreCost || Credit < creditCost)
            {
                log = string.Format("资源不够");
                return false;
            }
            if (syn == BuildingSyntax.RL|| syn == BuildingSyntax.AC1 || syn == BuildingSyntax.AC2)
            {
                if (IsTechTileAnyGet())
                {
                    TechTilesGet++;
                }
            }
            //扣资源,执行操作
            Action queue = () =>
            {
                Ore -= oreCost;
                Credit -= creditCost;
                ReturnBuilding(map.HexArray[row, col].Building);
                map.HexArray[row, col].Building = build;
                RemoveBuilding(syn);
                GaiaGame.SetLeechPowerQueue(FactionName, row, col);
                if (trigger != null)
                {
                    TriggerRST(trigger);
                }
                if (trigger2 != null)
                {
                    TriggerRST(trigger2);
                }
                if (syn == BuildingSyntax.AC2)
                {
                    if (this is BalTak)
                    {
                        AddGameTiles(new BalTakBuilding.AC2());
                    }
                    else
                    {
                        AddGameTiles(new AC2());
                    }
                }
                else if (syn == BuildingSyntax.TC)
                {
                    TriggerRST(typeof(ATT5));
                }
                else if (syn == BuildingSyntax.SH)
                {
                    CallSpecialSHBuild();
                }
            };
            ActionQueue.Enqueue(queue);
            return true;
        }

        private bool IsTechTileAnyGet()
        {
            if (GameTileList.Count(x => x is StandardTechnology) != 9)
            {
                return true;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if ((((int)item.GetValue(this) == 4 || (int)item.GetValue(this) == 5) && GaiaGame.ATTList[i].isPicked == false)
                    && GameTileList.Exists(x => x is AllianceTile && x.IsUsed == false))
                {
                    return true;
                }
            }
            return false;
        }

        protected virtual void CallSpecialSHBuild()
        {
        }

        public int GetTechLevelbyIndex(int index)
        {
            switch (index)
            {
                case 0:return TransformLevel;
                case 1:return ShipLevel;
                case 2:return AILevel;
                case 3:return GaiaLevel;
                case 4:return EconomicLevel;
                case 5:return ScienceLevel;
                default:
                    throw new Exception("index越界"+index);
            }
        }

        public bool LeechPower(int power, FactionName factionFrom, bool isLeech)
        {
            var index = LeechPowerQueue.FindIndex(x => x.Item1 == power && x.Item2 == factionFrom);
            if (index == -1)
            {
                return false;
            }
            LeechPowerQueue.RemoveAt(index);
            if (isLeech)
            {
                //能吸的 
                var ret = Math.Min(Math.Min(m_powerToken1 * 2 + m_powerToken2, power), Score + 1);
                var actualpower = PowerIncrease(ret);
                Score -= Math.Max(actualpower - 1, 0);
            }
            return true;
        }

        private void RemoveBuilding(BuildingSyntax syn)
        {
            switch (syn)
            {
                case BuildingSyntax.M:
                    Mines.RemoveAt(0);
                    break;
                case BuildingSyntax.TC:
                    TradeCenters.RemoveAt(0);
                    break;
                case BuildingSyntax.RL:
                    ResearchLabs.RemoveAt(0);
                    break;
                case BuildingSyntax.AC1:
                    Academy1 = null;
                    break;
                case BuildingSyntax.AC2:
                    Academy2 = null;
                    break;
                case BuildingSyntax.SH:
                    StrongHold = null;
                    break;
                default:
                    throw new Exception(syn.ToString() + "不会被移除");
            }
        }

        public bool IsExitUnfinishFreeAction(out string log)
        {
            log = string.Empty;
            if (TerraFormNumber != 0 && !(IsUseAction2&&TerraFormNumber==1))
            {
                log = "还存在没使用的Transform";
                return true;
            }
            if (TempShip != 0)
            {
                log = "还存在没使用的QICSHIP";
                return true;
            }
            if (TechTilesGet > 0)
            {
                log = "还存在没拿取的科技版";
                return true;
            }else if (TechTilesGet < 0)
            {
                log = "板子拿多了喂！想干嘛兄弟";
                return true;
            }
            if (m_AllianceTileGet != 0)
            {
                log = "还存在没拿取的星盟版";
                return true;
            }
            if (TechReturn != 0)
            {
                log = "获取高级科技必须退回一个低级科技";
                return true;
            }
            if (PlanetGet != 0)
            {
                log = "死星没有被放置在地图上";
                return true;
            }
            if (TechTracAdv > 0)
            {
                log = "可以提升科技,请选择一项科技提升,如果不需要提升科技请使用-advance语句";
                return true;
            }
            if (TechTracAdv <-1)
            {
                log = "科技升级多咯";
                return true;
            }
            if (TechTracAdv == -1 && !IsNoAdvTechTrack)
            {
                log = "多升级了一次科技";
                return true;
            }
            if (AllianceTileReGet != 0)
            {
                log = "选择要重新积分的城版";
                return true;
            }
            if (FactionSpecialAbility < 0)
            {
                log = "貌似多用了SH能力?";
                return true;
            }
            if (FactionSpecialAbility > 0)
            {
                log = "SH能力别忘了使用啊";
                return true;
            }
            if (AllianceTileCost > GameTileList.Count(x => x is AllianceTile && x.IsUsed == false))
            {
                log = "指令消耗的星盟板块数量大于拥有的";
                return true;
            }
            return false;
        }


        public virtual void ResetUnfinishAction()
        {
            ActionQueue.Clear();
            UnDoActionQueue.Clear();
            TerraFormNumber = 0;
            TempShip = 0;
            m_TechTilesGet = 0;
            m_TechTrachAdv = 0;
            m_AllianceTileGet = 0;
            LimitTechAdvance = string.Empty;
            IsUseAction2 = false;
            TempPowerToken1 = 0;
            TempPowerToken2 = 0;
            TempPowerToken3 = 0;
            TempPowerTokenGaia = 0;
            TempCredit = 0;
            TempKnowledge = 0;
            TempOre = 0;
            TempQICs = 0;
            TechReturn = 0;
            PlanetGet = 0;
            IsSingleAdvTechTrack = false;
            IsNoAdvTechTrack = false;
            AllianceTileReGet = 0;
            PlanetAlready = false;
            FactionSpecialAbility = 0;
            AllianceTileCost = 0;
        }



        static List<string> TechStrList = new List<string>(){
            "tf",
            "ship",
            "ai",
            "gaia",
            "eco",
            "sci",
        };

#region 临时变量 退出前需要清除
        public bool IsUseAction2 { get; set; }
        /// <summary>
        /// 代表需要退回的板子数量
        /// </summary>
        public int TechReturn { get; set; }
        public int PlanetGet { get; set; }
        public bool IsSingleAdvTechTrack { get; set; }
        public bool IsNoAdvTechTrack { get; set; }
        public bool PlanetAlready { get; set; }
        public int FactionSpecialAbility { get; set; }
        /// <summary>
        /// 记录两个FST计分板分数的
        /// </summary>
        public int FinalEndScore { get; set; }
        #endregion

        public static string ConvertTechIndexToStr(int v)
        {
            return TechStrList[v];
        }

        public virtual void IncreaseTech(string tech)
        {
            switch (tech)
            {
                case "tf":
                    m_TransformLevel++;
                    if (m_TransformLevel == 1)
                    {
                        Ore += 2;
                    }
                    else if (m_TransformLevel == 3)
                    {
                        PowerIncrease(GameConstNumber.TechLv2toLv3BonusPower);
                    }
                    else if (m_TransformLevel == 4)
                    {
                        Ore += 2;
                    }
                    else if (m_TransformLevel == 5)
                    {
                        GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                        GameTileList.Add(GaiaGame.AllianceTileForTransForm);
                        GaiaGame.AllianceTileForTransForm.OneTimeAction(this);
                        TriggerRST(typeof(RST5));
                    }
                    break;
                case "ai":
                    m_AILevel++;
                    if (m_AILevel == 1 || m_AILevel == 2)
                    {
                        QICs += 1;
                    }else if (m_AILevel == 3)
                    {
                        QICs += 2;
                        PowerIncrease(GameConstNumber.TechLv2toLv3BonusPower);
                    }else if (m_AILevel == 4)
                    {
                        QICs += 2;
                    }
                    else if(m_AILevel==5)
                    {
                        GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                        QICs += 4;
                    }
                    break;
                case "eco":
                    m_EconomicLevel++;
                    if (m_EconomicLevel==3)
                    {
                        PowerIncrease(GameConstNumber.TechLv2toLv3BonusPower);
                    }else if (m_EconomicLevel == 5)
                    {
                        GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                        Ore += 3;
                        Credit += 6;
                        PowerIncrease(6);
                    }
                    break;
                case "gaia":
                    m_GaiaLevel++;
                    if (m_GaiaLevel == 1)
                    {
                        Gaias.Add(new GaiaBuilding());
                    }else if(m_GaiaLevel == 2)
                    {
                        PowerToken1 += 3;
                    }
                    else if (m_GaiaLevel == 3)
                    {
                        PowerIncrease(GameConstNumber.TechLv2toLv3BonusPower);
                        Gaias.Add(new GaiaBuilding());
                    }
                    else if (m_GaiaLevel == 4)
                    {
                        Gaias.Add(new GaiaBuilding());
                    }
                    else if(m_GaiaLevel ==5)
                    {
                        GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                        Score +=4;
                        Score += GaiaPlanetNumber * 1;
                    }
                    break;
                case "sci":
                    m_ScienceLevel++;
                    if (m_ScienceLevel == 3)
                    {
                        PowerIncrease(GameConstNumber.TechLv2toLv3BonusPower);
                    }
                    else if (m_ScienceLevel == 5)
                    {
                        GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                        Knowledge += 9;
                    }
                    break;
                case "ship":
                    m_ShipLevel++;
                    if (m_ShipLevel == 1)
                    {
                        QICs += 1;
                    }
                    else if (m_ShipLevel == 3)
                    {
                        QICs += 1;
                        PowerIncrease(GameConstNumber.TechLv2toLv3BonusPower);
                    }
                    else if (m_ShipLevel == 5)
                    {
                        GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                        //throw new NotImplementedException("黑星科技没有完成");
                    }
                    break;
                default:
                    throw new Exception("不存在此科技条" + tech);
            }
            TriggerRST(typeof(RST6));
            TriggerRST(typeof(ATT6));
            return;
        }

        internal void RemoveGameTiles(StandardTechnology tile)
        {
            GameTileList.Remove(tile);
            if (tile.CanAction)
            {
                PredicateActionList.Remove(tile.GetType().Name.ToLower());
                ActionList.Remove(tile.GetType().Name.ToLower());
            }
            if (tile is STT9)
            {
                (tile as STT9).ReturnGameTile(this);
            }
        }

        public GameTiles GameTileGet(string str)
        {
            var ret= GameTileList.Find(x => x.GetType().Name.Equals(str, StringComparison.OrdinalIgnoreCase));
            return ret;
        }

        public virtual void ForgingAllianceGetTiles(List<Tuple<int, int>> list)
        {
            Action action = () =>
            {
                list.ForEach(x =>
                {
                    if (GaiaGame.Map.HexArray[x.Item1, x.Item2].TFTerrain == Terrain.Empty)
                    {
                        GaiaGame.Map.HexArray[x.Item1, x.Item2].AddSatellite(FactionName);
                    }
                    else if (this is Lantida && GaiaGame.Map.HexArray[x.Item1, x.Item2].SpecialBuilding != null)
                    {
                        GaiaGame.Map.HexArray[x.Item1, x.Item2].IsSpecialBuildingAlliance = true;
                    }
                    else
                    {
                        GaiaGame.Map.HexArray[x.Item1, x.Item2].IsAlliance = true;
                    }
                });
                if(this is Hive)
                {
                    QICs -= list.Sum(x => GaiaGame.Map.HexArray[x.Item1, x.Item2].TFTerrain == Terrain.Empty ? 1 : 0);
                }
                else
                {
                    RemovePowerToken(list.Sum(x => GaiaGame.Map.HexArray[x.Item1, x.Item2].TFTerrain == Terrain.Empty ? 1 : 0));
                }
                TriggerRST(typeof(RST5));
            };

            ActionQueue.Enqueue(action);
            m_AllianceTileGet++;
        }

        public virtual bool BuildBlackPlanet(int row, int col, out string log)
        {
            log = string.Empty;
            var hex = GaiaGame.Map.HexArray[row, col];
            if (hex == null)
            {
                log = "出界了兄弟";
                return false;
            }
            if (hex.TFTerrain != Terrain.Empty)
            {
                log = "死星必须建立在太空中";
                return false;
            }
            if (hex.Satellite.Any())
            {
                log = "不能建在别人的卫星上";
                return false;
            }
            var distanceNeed = GaiaGame.Map.CalShipDistanceNeed(row, col, FactionName);
            TempShip++;
            var qicship = Math.Max((distanceNeed - GetShipDistance + 1) / 2, 0);
            if (QICs * 2 < distanceNeed - GetShipDistance)
            {
                log = string.Format("死星距离太偏远了,需要{0}个Q来加速", Math.Max((distanceNeed - GetShipDistance + 1) / 2, 0));
                return false;
            }


            Action action = () =>
            {
                hex.TFTerrain = Terrain.Black;
                hex.Building = new Mine();
                hex.FactionBelongTo = FactionName;
                FactionName factionName = FactionName;
                var surroundhex = GaiaGame.Map.GetSurroundhexWithBuild(row, col, FactionName);
                //遍历相邻的点
                foreach (var tuple in surroundhex)
                {
                    TerrenHex item = GaiaGame.Map.HexArray[tuple.Item1, tuple.Item2];
                    //如果不是联邦
                    if (!item.IsAlliance)
                    {
                        continue;
                    }
                    //亚特兰蒂斯种族需要特殊建筑
                    if (factionName == FactionName.Lantida)
                    {
                        //挨到亚特兰蒂斯
                        if (item.FactionBelongTo == FactionName.Lantida || (item.IsSpecialBuildingAlliance))
                        {
                            GaiaGame.Map.HexArray[row, col].IsAlliance = true;
                        }
                    }
                    else
                    {
                        GaiaGame.Map.HexArray[row, col].IsAlliance = true;
                    }
                }
                
                QICs -= qicship;
                GaiaGame.SetLeechPowerQueue(FactionName, row, col);
                TriggerRST(typeof(RST1));
                TriggerRST(typeof(ATT4));
            };
            ActionQueue.Enqueue(action);
            TempShip = 0;
            return true;
        }

        public virtual bool ForgingAllianceCheck(List<Tuple<int, int>> list, out string log)
        {
            log = string.Empty;
            var map = GaiaGame.Map;
            var SatelliteHexList = list.Where(x => GaiaGame.Map.HexArray[x.Item1, x.Item2].TFTerrain == Terrain.Empty && !GaiaGame.Map.HexArray[x.Item1, x.Item2].Satellite.Contains(FactionName))
                                    .ToList();
            var BuildingHexList = list.Where(x =>
                                              {
                                                  TerrenHex terrenHex = GaiaGame.Map.HexArray[x.Item1, x.Item2];
                                                  return terrenHex.Building != null && terrenHex.FactionBelongTo == FactionName && !(terrenHex.Building is GaiaBuilding);
                                              })
                                     .ToList();

            if ((SatelliteHexList.Count + BuildingHexList.Count) != list.Count)
            {
                log = "形成星盟的只能是本家的非盖亚建筑物以及未放置本家卫星的空地";
                return false;
            }
            if (PowerToken1 + PowerToken2 + PowerToken3 < SatelliteHexList.Count)
            {
                log = string.Format("能量豆总数量为{0},放卫星数量为{1},能量豆不够", (PowerToken1 + PowerToken2 + PowerToken3), SatelliteHexList.Count);
                return false;
            }
            if (list.Exists(x =>
            {
                var surroundHex = GaiaGame.Map.GetSurroundhexWithBuildingAndSatellite(x.Item1, x.Item2, FactionName, list: list);
                return surroundHex.Exists(y => map.GetHex(y).IsAlliance
                || (map.GetHex(y).Satellite != null && map.GetHex(y).Satellite.Contains(FactionName))
                || (map.GetHex(y).FactionBelongTo == FactionName && !(map.GetHex(y).Building is GaiaBuilding)));
            }))
            {
                log = "周围接壤的地块也不能形成过星盟不能有自己种族的卫星不能有自己的建筑";
                return false;
            }

            if (list.Exists(x =>
            {
                TerrenHex terrenHex = GaiaGame.Map.HexArray[x.Item1, x.Item2];
                return terrenHex.IsAlliance == true;
            }))
            {
                log = "有地块已经形成过星盟";
                return false;
            }
            var TerrenGroup = new List<List<Tuple<int, int>>>();
            foreach (var item in list)
            {
                var Dis1List = TerrenGroup.FindAll(x => x.Exists(y => map.CalTwoHexDistance(y.Item1, y.Item2, item.Item1, item.Item2) == 1));
                if (Dis1List.Any())
                {
                    var sum = new List<Tuple<int, int>>();
                    Dis1List.ForEach(x => {
                        sum.AddRange(x);
                        TerrenGroup.Remove(x);
                    });
                    sum.Add(item);
                    TerrenGroup.Add(sum);
                }
                else
                {
                    TerrenGroup.Add(new List<Tuple<int, int>>() { item });
                }
            }
            if (TerrenGroup.Count != 1)
            {
                log = "选择的所有点必须连通";
                return false;
            }

            TerrenGroup = new List<List<Tuple<int, int>>>();
            foreach (var item in BuildingHexList)
            {
                var Dis1List = TerrenGroup.FindAll(x => x.Exists(y => map.CalTwoHexDistance(y.Item1, y.Item2, item.Item1, item.Item2) == 1));
                if (Dis1List.Any())
                {
                    var sum = new List<Tuple<int, int>>();
                    Dis1List.ForEach(x => {
                        sum.AddRange(x);
                        TerrenGroup.Remove(x);
                    });
                    sum.Add(item);
                    TerrenGroup.Add(sum);
                }
                else
                {
                    TerrenGroup.Add(new List<Tuple<int, int>>() { item });
                }
            }

            if (BuildingHexList.Sum(x =>
            {
                if (this is MadAndroid && StrongHold == null && OGTerrain == GaiaGame.Map.HexArray[x.Item1, x.Item2].TFTerrain)
                {
                    return GaiaGame.Map.HexArray[x.Item1, x.Item2].Building.MagicLevel + 1;
                }
                else
                {
                    return GaiaGame.Map.HexArray[x.Item1, x.Item2].Building.MagicLevel;
                }
            }) < m_allianceMagicLevel)
            {
                log = string.Format("能量等级不够{0}级", m_allianceMagicLevel);
                return false;
            }

            //foreach(var item in TerrenGroup)
            //{
            //    var temp = new List<List<Tuple<int, int>>>(TerrenGroup);
            //    temp.Remove(item);
            //    if (temp.Sum(y => y.Sum(x => GaiaGame.Map.HexArray[x.Item1, x.Item2].Building.MagicLevel)) >= m_allianceMagicLevel)
            //    {
            //        log = string.Format("即使不用建筑{0}也能出城，这不符合规则", string.Join(",", item.Select(x => IntExtensions.ConvertPosToStr(x))));
            //        return false;
            //    }
            //}
            //var count = 0;
            //if (SatelliteHexList.Count != 0)
            //{
            //    var StartHexList = TerrenGroup.First();
            //    TerrenGroup.Remove(StartHexList);
            //    var NewFarHexList = new List<Tuple<int, int>>(StartHexList);
            //    var NextHexList = new List<Tuple<int, int>>();
            //    while (TerrenGroup.Count != 0)
            //    {

            //        NewFarHexList.ForEach(x =>
            //        {
            //            var suround = map.GetSurroundhex(x.Item1, x.Item2, FactionName, BuildingHexList);
            //            NextHexList.AddRange(suround.Where(y => !StartHexList.Contains(y)));
            //        });
            //        NewFarHexList.Clear();
            //        var tempTerrenGroup = new List<List<Tuple<int, int>>>(TerrenGroup.ToList());
            //        foreach (var item in tempTerrenGroup)
            //        {
            //            if (item.Exists(x => map.GetSurroundhex(x.Item1, x.Item2, FactionName, BuildingHexList).Exists(y => NextHexList.Contains(y))))
            //            {
            //                StartHexList.AddRange(item);
            //                TerrenGroup.Remove(item);
            //                NewFarHexList.AddRange(item);
            //            }
            //        }
                    
            //        NewFarHexList.AddRange(NextHexList);
            //        StartHexList.AddRange(NextHexList);
            //        NextHexList.Clear();
            //        count++;
            //    }
            //}
            //if(count!= SatelliteHexList.Count)
            //{
            //    log = string.Format("只要{0}个卫星就能出城,请检查卫星方法", count);
            //    return false;
            //}
            

            return true;
        }

        public virtual bool ConvertOneResourceToAnother(int rFNum, string rFKind, int rTNum, string rTKind, out string log, int? rTNum2 = null, string rTKind2 = null,int? rFNum2=null,string rFKind2=null)
        {
            log = string.Empty;
            if (rFNum2 != null || rFKind2 != null)
            {
                log = "多种资源同时换取其他资源是Taklons专用语句";
                return false;
            }
            if (rTNum2 != null || rTKind2 != null)
            {
                log = "pw同时换取多项资源Nevla专用语句";
                return false;
            }
            var str = rFKind + rTKind;
            switch (str)
            {
                case "pwq":
                    if (rFNum != rTNum * 4)
                    {
                        log = "兑换比例为4：1";
                        return false;
                    }
                    if (PowerToken3 < rFNum)
                    {
                        log = "能量值不够";
                        return false;
                    }
                    TempPowerToken3 -= rFNum;
                    TempPowerToken1 += rFNum;
                    TempQICs += rTNum;
                    Action action = () =>
                    {
                        PowerToken3 = PowerToken3;
                        PowerToken1 = PowerToken1;
                        QICs = QICs;
                        TempPowerToken3 = 0;
                        TempPowerToken1 = 0;
                        TempQICs = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "pwo":
                    if (rFNum != rTNum * 3)
                    {
                        log = "兑换比例为3：1";
                        return false;
                    }
                    if (PowerToken3 < rFNum)
                    {
                        log = "能量值不够";
                        return false;
                    }
                    TempPowerToken3 -= rFNum;
                    TempPowerToken1 += rFNum;
                    TempOre += rTNum;
                    action = () =>
                    {
                        PowerToken3 = PowerToken3;
                        PowerToken1 = PowerToken1;
                        Ore = Ore;
                        TempPowerToken3 = 0;
                        TempPowerToken1 = 0;
                        TempOre = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "pwk":
                    if (rFNum != rTNum * 4)
                    {
                        log = "兑换比例为4：1";
                        return false;
                    }
                    if (PowerToken3 < rFNum)
                    {
                        log = "能量值不够";
                        return false;
                    }
                    TempPowerToken3 -= rFNum;
                    TempPowerToken1 += rFNum;
                    TempKnowledge += rTNum;
                    action = () =>
                    {
                        PowerToken3 = PowerToken3;
                        PowerToken1 = PowerToken1;
                        Knowledge = Knowledge;
                        TempPowerToken3 = 0;
                        TempPowerToken1 = 0;
                        TempKnowledge = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "pwc":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (PowerToken3 < rFNum)
                    {
                        log = "能量值不够";
                        return false;
                    }
                    TempPowerToken3 -= rFNum;
                    TempPowerToken1 += rFNum;
                    TempCredit += rTNum;
                    action = () =>
                    {
                        PowerToken3 = PowerToken3;
                        PowerToken1 = PowerToken1;
                        Credit = Credit;
                        TempPowerToken3 = 0;
                        TempPowerToken1 = 0;
                        TempCredit = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "qo":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (QICs < rFNum)
                    {
                        log = "Q不够";
                        return false;
                    }
                    TempQICs -= rFNum;
                    TempOre += rTNum;
                    action = () =>
                    {
                        QICs = QICs;
                        Ore = Ore;
                        TempQICs = 0;
                        TempOre = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "kc":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (Knowledge < rFNum)
                    {
                        log = "知识不够";
                        return false;
                    }
                    TempKnowledge -= rFNum;
                    TempCredit += rTNum;
                    action = () =>
                    {
                        Knowledge = Knowledge;
                        Credit = Credit;
                        TempKnowledge = 0;
                        TempCredit = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "oc":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (Ore < rFNum)
                    {
                        log = "矿不够";
                        return false;
                    }
                    TempOre -= rFNum;
                    TempCredit += rTNum;
                    action = () =>
                    {
                        Ore = Ore;
                        Credit = Credit;
                        TempOre = 0;
                        TempCredit = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "opwt":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (Ore < rFNum)
                    {
                        log = "矿不够";
                        return false;
                    }
                    TempOre -= rFNum;
                    TempPowerToken1 += rTNum;
                    action = () =>
                    {
                        Ore = Ore;
                        PowerToken1 = PowerToken1;
                        TempOre = 0;
                        TempPowerToken1 = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "qc":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (Ore < rFNum)
                    {
                        log = "Q不够";
                        return false;
                    }
                    TempQICs -= rFNum;
                    TempCredit += rTNum;
                    action = () =>
                    {
                        QICs = QICs;
                        Credit = Credit;
                        TempQICs = 0;
                        TempCredit = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                case "qpwt":
                    if (rFNum != rTNum * 1)
                    {
                        log = "兑换比例为1：1";
                        return false;
                    }
                    if (QICs < rFNum)
                    {
                        log = "q不够";
                        return false;
                    }
                    TempQICs -= rFNum;
                    TempPowerToken1 += rTNum;
                    action = () =>
                    {
                        QICs = QICs;
                        PowerToken1 = PowerToken1;
                        TempQICs = 0;
                        TempPowerToken1 = 0;
                    };
                    ActionQueue.Enqueue(action);
                    break;
                default:
                    log = "不支持这种转换";
                    return false;
            }
            return true;
        }

        internal void GetResouceChange(FactionBackup turnStartBackup)
        {
            if (turnStartBackup == null)
            {
                return;
            }
            turnStartBackup.m_score = Score - turnStartBackup.m_score;
            turnStartBackup.m_credit = m_credit - turnStartBackup.m_credit;
            turnStartBackup.m_ore = m_ore - turnStartBackup.m_ore;
            turnStartBackup.m_QICs = m_QICs - turnStartBackup.m_QICs;
            turnStartBackup.m_knowledge = m_knowledge - turnStartBackup.m_knowledge;
            turnStartBackup.m_powerToken1 = m_powerToken1 - turnStartBackup.m_powerToken1;
            turnStartBackup.m_powerToken2 = m_powerToken2 - turnStartBackup.m_powerToken2;
            turnStartBackup.m_powerToken3 = m_powerToken3 - turnStartBackup.m_powerToken3;
            turnStartBackup.m_powerTokenGaia = m_powerTokenGaia - turnStartBackup.m_powerTokenGaia;
            return;
        }

        public bool GetAllianceTile(AllianceTile alt, out string log)
        {
            log = string.Empty;
            if (m_AllianceTileGet == 0)
            {
                log = "没有获得城版的权限";
                return false;
            }
            Action action = () =>
            {
                GameTileList.Add(alt);
                alt.OneTimeAction(this);
                GaiaGame.ALTList.Remove(alt);
            };
            ActionQueue.Enqueue(action);
            m_AllianceTileGet--;
            return true;
        }

        public bool PredicateAction(string actionStr, out string log)
        {
            log = string.Empty;
            if (PredicateActionList.ContainsKey(actionStr)&& !PredicateActionList[actionStr].Invoke(this))
            {
                log = "此行动不可用";
                return false;
            }
            if (!ActionList.ContainsKey(actionStr))
            {
                log = "没有做此行动的板子";
                return false;
            }
            return true;
        }

        public void DoAction(string actionStr,bool isFreeSyntax=false)
        {
            var func = ActionList[actionStr];
            if ("stt1".Equals(actionStr))
            {
                ImmediAction(actionStr);
            }
            else if (isFreeSyntax)
            {
                func.Invoke(this);
            }
            else
            {
                Action action = () =>
                {
                    func.Invoke(this);
                };
                ActionQueue.Enqueue(action);
            }
        }

        private void ImmediAction(string actionStr)
        {
            var func = ActionList[actionStr];
            func.Invoke(this);
            UnDoActionQueue.Enqueue(() =>
            {
                GameTileGet(actionStr).UndoGameTileAction(this);
            }
            );
        }

        public bool IsIncreateTechValide(string tech, out string log, bool isIncreaseAllianceTileCost = false)
        {
            log = string.Empty;
            var index = TechStrList.FindIndex(x => x.Equals(tech));
            return IsIncreaseTechLevelByIndexValidate(index, out log, isIncreaseAllianceTileCost);
        }

        private void ReturnBuilding(Building building)
        {
            switch (building.GetType().Name)
            {
                case "Mine":
                    Mines.Add(building as Mine);
                    break;
                case "TradeCenter":
                    TradeCenters.Add(building as TradeCenter);
                    break;
                case "ResearchLab":
                    ResearchLabs.Add(building as ResearchLab);
                    break;
                default:
                    throw new Exception(building.GetType().ToString() + "不会被归还");
            }
        }
    }
}
