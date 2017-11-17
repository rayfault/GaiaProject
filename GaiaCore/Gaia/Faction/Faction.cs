﻿using GaiaCore.Gaia.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GaiaCore.Gaia
{
    public abstract partial class Faction
    {
        public Faction(FactionName name, GaiaGame gg)
        {
            FactionName = name;
            if (gg == null)
            {
                gg = new GaiaGame(new string[] { });
                return;
            }
            if (gg.IsTestGame)
            {
                m_credit = 50;
                m_knowledge = 20;
                m_ore = 30;
                m_QICs = 4;
                m_powerToken3 = 30;
                m_ShipLevel = 4;
            }
            else
            {
                m_credit = 15;
                m_knowledge = 3;
                m_ore = 4;
                m_QICs = 1;
                m_powerToken3 = 0;
                m_ShipLevel = 0;
            }


            m_powerToken1 = 2;
            m_powerToken2 = 4;
            m_powerTokenGaia = 0;
            m_TransformLevel = 0;
            m_AILevel = 0;
            m_EconomicLevel = 0;
            m_GaiaLevel = 0;
            m_ScienceLevel = 0;

            Mines = new List<Mine>();
            for (int i = 0; i < GameConstNumber.MineCount; i++)
            {
                Mines.Add(new Mine());
            }
            TradeCenters = new List<TradeCenter>();
            for (int i = 0; i < GameConstNumber.TradeCenterCount; i++)
            {
                TradeCenters.Add(new TradeCenter());
            }
            ResearchLabs = new List<ResearchLab>();
            for (int i = 0; i < GameConstNumber.ResearchLabCount; i++)
            {
                ResearchLabs.Add(new ResearchLab());
            }
            Academy1 = new Academy();
            Academy2 = new Academy();
            StrongHold = new StrongHold();
            Gaias = new List<GaiaBuilding>();
            GameTileList = new List<GameTiles>();
            LeechPowerQueue = new List<Tuple<int, FactionName>>();
            Score = 10;
            GaiaGame = gg;
            ActionQueue = new Queue<Action>();
            ActionList = new Dictionary<string, Func<Faction, bool>>();
            PredicateActionList = new Dictionary<string, Func<Faction, bool>>();
            GaiaGame.MapActionMrg.AddMapActionList(ActionList, PredicateActionList);
            GaiaPlanetNumber = 0;
            m_allianceMagicLevel = 7;
            PowerPreview = new List<Tuple<int, int, int>>();
        }

        public virtual bool FinishIntialMines()
        {
            if (GameConstNumber.MineCount - Mines.Count == 2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public FactionName FactionName { get; }
        public List<GameTiles> GameTileList { set; get; }
        private FactionBackup backup;
        //八种资源
        protected int m_credit;
        protected int m_ore;
        protected int m_knowledge;
        protected int m_QICs;
        protected int m_powerToken1;
        protected int m_powerToken2;
        protected int m_powerToken3;
        protected int m_powerTokenGaia;
        //六种科技
        protected int m_TransformLevel;
        protected int m_ShipLevel;
        protected int m_AILevel;
        protected int m_GaiaLevel;
        protected int m_EconomicLevel;
        protected int m_ScienceLevel;
        //出城需要的魔力等级
        protected int m_allianceMagicLevel;

        protected const int m_MineCount = 8;
        protected const int m_MineOreCost = 1;
        protected const int m_MineCreditCost = 2;
        //一些不会变的常量
        protected const int m_TradeCenterOreCost = 2;
        protected const int m_TradeCenterCreditCostCluster = 3;
        protected const int m_TradeCenterCreditCostAlone = 6;
        protected const int m_TradeCenterCount = 4;
        protected const int m_ReaserchLabOreCost = 3;
        protected const int m_ReaserchLabCreditCost = 5;
        protected const int m_ReaserchLabCount = 3;
        protected const int m_AcademyOreCost = 6;
        protected const int m_AcademyCreditCost = 6;
        protected const int m_AcademyCount = 2;
        protected const int m_StrongHoldOreCost = 4;
        protected const int m_StrongHoldCreditCost = 6;
        protected const int m_StrongHoldCount = 1;
        #region 展示用变量 不参与逻辑计算
        /// <summary>
        /// 中文名称
        /// </summary>
        public string ChineseName { get; set; }
        public string UserName { get; set; }

        public string[] colorList = new string[] { "#16a0e0", "#d71729", "#d75d0c", "#deb703", "#8b3a0e", "#6b6868", "#ebfafb" };
        public string[] colorMapList = new string[] { "#6bd8f3", "#f23c4d", "#ea8736", "#facd2f", "#ad5e2f", "#a3a3a3", "#d3f1f5" };
        /// <summary>
        /// 种族颜色代码
        /// </summary>
        public string ColorCode { get; set; }
        /// <summary>
        /// 地图颜色
        /// </summary>
        public string ColorMap { get; set; }
        #endregion

        #region 临时变量 判断动作完成与否 需要清零
        private int m_TerraFormNumber = 0;
        private int m_TempShip = 0;
        private int m_TechTilesGet = 0;
        private int m_TechTrachAdv = 0;
        private int m_AllianceTileGet = 0;
        private int m_AllianceTileReGet = 0;
        public int TempPowerToken1 = 0;
        public int TempPowerToken2 = 0;
        public int TempPowerToken3 = 0;
        public int TempPowerTokenGaia = 0;

        public int TempCredit = 0;
        public int TempOre = 0;
        public int TempKnowledge = 0;
        public int TempQICs = 0;
        public string LimitTechAdvance { get; set; }
        public int AllianceTileCost = 0;
        #endregion

        public virtual int PowerIncrease(int i)
        {
            var ret = Math.Min(m_powerToken1 * 2 + m_powerToken2, i);
            if (m_powerToken1 > i)
            {
                m_powerToken1 -= i;
                m_powerToken2 += i;
            }
            else if (m_powerToken1 * 2 + m_powerToken2 > i)
            {
                m_powerToken2 = m_powerToken2 - i + m_powerToken1 * 2;
                m_powerToken3 += i - m_powerToken1;
                m_powerToken1 = 0;
            }
            else
            {
                m_powerToken3 += m_powerToken1 + m_powerToken2;
                m_powerToken2 = 0;
                m_powerToken1 = 0;
            }
            return ret;
        }
        public List<GaiaBuilding> Gaias { set; get; }
        public List<Mine> Mines { set; get; }
        public List<TradeCenter> TradeCenters { set; get; }
        public List<ResearchLab> ResearchLabs { set; get; }
        public Academy Academy1 { set; get; }
        public Academy Academy2 { set; get; }
        public StrongHold StrongHold { set; get; }
        public int Credit { get => m_credit + TempCredit; set { m_credit = value; TempCredit = 0; } }
        public int Ore { get => m_ore + TempOre; set { m_ore = value; TempOre = 0; } }
        public int Knowledge { get => m_knowledge + TempKnowledge; set { m_knowledge = value; TempKnowledge = 0; } }
        public int QICs { get => m_QICs + TempQICs; set { m_QICs = value; TempQICs = 0; } }
        public int PowerToken1 { get => m_powerToken1 + TempPowerToken1; set { m_powerToken1 = value; TempPowerToken1 = 0; } }
        public int PowerToken2 { get => m_powerToken2 + TempPowerToken2; set => m_powerToken2 = value; }
        public int PowerToken3 { get => m_powerToken3 + TempPowerToken3; set => m_powerToken3 = value; }
        public int TransformLevel { get => m_TransformLevel; }
        public int ShipLevel { get => m_ShipLevel; }
        public int AILevel { get => m_AILevel; }
        public int GaiaLevel { get => m_GaiaLevel; }
        public int EconomicLevel { get => m_EconomicLevel; }
        public int ScienceLevel { get => m_ScienceLevel; }
        public abstract Terrain OGTerrain { get; }
        public List<Tuple<int,int,int>> PowerPreview { set; get; }
        public List<Tuple<int, FactionName>> LeechPowerQueue { get; }
        public int Score { get; set; }
        public GaiaGame GaiaGame { get; }
        public Queue<Action> ActionQueue { get; set; }

        public Dictionary<string, Func<Faction, bool>> ActionList { get; set; }
        public Dictionary<string, Func<Faction, bool>> PredicateActionList { get; set; }
        public int GaiaPlanetNumber { get; set; }
        public int GetShipDistance
        {
            get
            {
                if (m_ShipLevel == 0 | m_ShipLevel == 1)
                {
                    return 1 + TempShip;
                }
                else if (m_ShipLevel == 2 | m_ShipLevel == 3)
                {
                    return 2 + TempShip;
                }
                else if (m_ShipLevel == 4)
                {
                    return 3 + TempShip;
                }
                else if (m_ShipLevel == 5)
                {
                    return 4 + TempShip;
                }
                throw new Exception("m_ShipLevel数值出错" + m_ShipLevel);
            }
        }

        public int GetTransformCost
        {
            get
            {
                if (m_TransformLevel == 0 || m_TransformLevel == 1)
                {
                    return 3;
                }
                else if (m_TransformLevel == 2)
                {
                    return 2;
                }
                else if (m_TransformLevel == 3 || m_TransformLevel == 4 || m_TransformLevel == 5)
                {
                    return 1;
                }
                throw new Exception("m_Transform数值出错" + m_TransformLevel);
            }
        }

        public int TechTilesGet { get => m_TechTilesGet; set => m_TechTilesGet = value; }
        public int TechTracAdv { get => m_TechTrachAdv; set => m_TechTrachAdv = value; }
        public int TerraFormNumber { get => m_TerraFormNumber; set => m_TerraFormNumber = value; }
        public int TempShip { get => m_TempShip; set => m_TempShip = value; }

        public int PowerTokenGaia { get => m_powerTokenGaia + TempPowerTokenGaia; set { m_powerTokenGaia = value; TempPowerTokenGaia = 0; } }
        public int AllianceTileReGet { get => m_AllianceTileReGet; set => m_AllianceTileReGet = value; }

        private static List<FieldInfo> list = new List<FieldInfo>()
        {
            typeof(Faction).GetField("m_TransformLevel",BindingFlags.NonPublic|BindingFlags.Instance),
            typeof(Faction).GetField("m_ShipLevel",BindingFlags.NonPublic|BindingFlags.Instance),
            typeof(Faction).GetField("m_AILevel",BindingFlags.NonPublic|BindingFlags.Instance),
            typeof(Faction).GetField("m_GaiaLevel",BindingFlags.NonPublic|BindingFlags.Instance),
            typeof(Faction).GetField("m_EconomicLevel",BindingFlags.NonPublic|BindingFlags.Instance),
            typeof(Faction).GetField("m_ScienceLevel",BindingFlags.NonPublic|BindingFlags.Instance),
        };


        public bool IsIncreaseTechLevelByIndexValidate(int index, out string log, bool isIncreaseAllianceTileCost = false)
        {
            log = string.Empty;
            if (index < 0 | index > 5)
            {
                throw new Exception("超出科技条边界");
            }
            var level = (int)list[index].GetValue(this);
            if (level >= 0 && level < 4)
            {
                //level++;
            }
            else if (level == 4)
            {
                if (GaiaGame.FactionList.Exists(x => (int)list[index].GetValue(x) == 5))
                {
                    log = "已经有人登顶了,只有一人能登顶";
                    return false;
                }
                if (!GameTileList.Exists(x => x is AllianceTile && x.IsUsed == false))
                {
                    log = "需要星盟版(ALT)才能继续升级";
                    return false;
                }
                if (isIncreaseAllianceTileCost)
                {
                    AllianceTileCost++;
                }
                return true;
            }
            else
            {
                log = "满级情况不能继续升级";
                return false;
            }
            return true;
        }

        public void AddGameTiles(GameTiles tile)
        {
            GameTileList.Add(tile);
            if (tile.CanAction)
            {
                PredicateActionList.Add(tile.GetType().Name.ToLower(), tile.PredicateGameTileAction);
                ActionList.Add(tile.GetType().Name.ToLower(), tile.InvokeGameTileAction);
            }
            tile.OneTimeAction(this);
        }
        //private int m_TransformLevel;

        //       private int m_ShipLevel;
        //       private int m_AILevel;
        //       private int m_GaiaLevel;
        //       private int m_EconomicLevel;
        //       private int m_ScienceLevel;
        private void BackupResource()
        {
            backup = new FactionBackup()
            {
                m_credit = m_credit,
                m_knowledge = m_knowledge,
                m_ore = m_ore,
                m_powerToken1 = m_powerToken1,
                m_powerToken2 = m_powerToken2,
                m_powerToken3 = m_powerToken3,
                m_powerTokenGaia = m_powerTokenGaia,
                m_QICs = m_QICs
            };
        }

        private void RestoreResource()
        {
            if (backup != null)
            {
                m_credit = backup.m_credit;
                m_knowledge = backup.m_knowledge;
                m_ore = backup.m_ore;
                m_powerToken1 = backup.m_powerToken1;
                m_powerToken2 = backup.m_powerToken2;
                m_powerToken3 = backup.m_powerToken3;
                m_powerTokenGaia = backup.m_powerTokenGaia;
                m_QICs = backup.m_QICs;
            };
        }

        public Dictionary<string, int> CalNextTurnIncome()
        {
            var ret = new Dictionary<string, int>();
            var tile = GameTileList.Find(x => x is RoundBooster);
            if (tile != null)
            {
                GameTileList.Remove(tile);
            }
            ret.Add("C", CalCreditIncome());
            ret.Add("K", CalKnowledgeIncome());
            ret.Add("O", CalOreIncome());
            ret.Add("Q", CalQICIncome());
            ret.Add("PWT", CalPowerTokenIncome());
            ret.Add("PW", CalPowerIncome());
            if (tile != null)
            {
                GameTileList.Add(tile);
            }
            return ret;
        }

        public virtual void CalIncome()
        {
            Ore += CalOreIncome();
            Credit += CalCreditIncome();
            Knowledge += CalKnowledgeIncome();
            //PowerIncrease(CalPowerIncome());
            //PowerToken1 += CalPowerTokenIncome();
            QICs += CalQICIncome();
        }



        protected virtual int CalOreIncome()
        {
            var ret = 0;
            if (Mines.Count >= 6 && Mines.Count <= 8)
            {
                ret += 9 - Mines.Count;
            }
            else if (Mines.Count == 5)
            {
                ret += 3;
            }
            else if (Mines.Count < 5)
            {
                ret += 8 - Mines.Count;
            }

            ret += GameTileList.Sum(x => x.GetOreIncome());

            switch (EconomicLevel)
            {
                case 2:
                    ret += 1;
                    break;
                case 3:
                    ret += 1;
                    break;
                case 4:
                    ret += 2;
                    break;
                case 5:
                    ret += 3;
                    break;
                default:
                    break;
            }
            return ret;
        }
        protected virtual int CalCreditIncome()
        {
            int ret = 0;
            if (TradeCenters.Count == 3)
            {
                ret += 3;
            }
            else if (TradeCenters.Count == 2)
            {
                ret += 7;
            }
            else if (TradeCenters.Count == 1)
            {
                ret += 11;
            }
            else if (TradeCenters.Count == 0)
            {
                ret += 16;
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


        protected virtual int CalKnowledgeIncome()
        {
            var ret = 0;
            ret += 4 - ResearchLabs.Count;
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

        protected virtual int CallAC1Income()
        {
            return 2;
        }

        protected virtual int CalQICIncome()
        {
            var ret = 0;
            ret += GameTileList.Sum(x => x.GetQICIncome());
            return ret;
        }

        protected virtual int CalPowerIncome(List<int> list = null)
        {
            List<int> ret;
            if (list != null)
            {
                ret = list;
            }
            else
            {
                ret = new List<int>();
            }

            if (StrongHold == null)
            {
                ret.Add(CallSHPowerIncome());
            }
            GameTileList.ForEach(x => ret.Add(x.GetPowerIncome()));
            switch (EconomicLevel)
            {
                case 1:
                    ret.Add(1);
                    break;
                case 2:
                    ret.Add(2);
                    break;
                case 3:
                    ret.Add(3);
                    break;
                case 4:
                    ret.Add(4);
                    break;
                case 5:
                    ret.Add(6);
                    break;
                default:
                    break;
            }
            return ret.Sum();
        }

        protected virtual int CalPowerTokenIncome(List<int> list = null)
        {
            List<int> ret;
            if (list != null)
            {
                ret = list;
            }
            else
            {
                ret = new List<int>();
            }
            GameTileList.ForEach(x => ret.Add(x.GetPowerTokenIncome()));
            if (StrongHold == null)
            {
                ret.Add(CallSHPowerTokenIncome());
            }
            return ret.Sum();
        }
        protected virtual int CallSHPowerIncome()
        {
            return 4;
        }
        protected virtual int CallSHPowerTokenIncome()
        {
            return 1;
        }

    }


}
