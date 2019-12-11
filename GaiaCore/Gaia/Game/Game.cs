﻿using System;
using System.Collections.Generic;
using System.Linq;
using GaiaCore.Gaia.Game;
using GaiaCore.Gaia.Tiles;
using GaiaCore.Util;
using GaiaDbContext.Models.AccountViewModels;
using GaiaDbContext.Models.HomeViewModels;
using GaiaProject.Data;
using Newtonsoft.Json;

namespace GaiaCore.Gaia
{
    /// <summary>
    /// 게임당 하나의 게임 인스턴스화
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class GaiaGame
    {
        private string m_TailLog=string.Empty;

        public GaiaGame(string[] username,string gameName = null)
        {
            this.GameName = gameName;
            UserCount = username.Count(x => !string.IsNullOrEmpty(x));
            GameStatus = new GameStatus();
            GameStatus.PlayerNumber = username.ToList().Where(x => !string.IsNullOrEmpty(x)).Count();
            FactionList = new List<Faction>();
            FactionNextTurnList = new List<Faction>();
            UserDic = new Dictionary<string, List<Faction>>();
            MapActionMrg = new MapActionMgr();
            Username = username;
            foreach(var us in username.ToList().Where(x=>!string.IsNullOrEmpty(x)).Distinct())
            {
                UserDic.Add(us, new List<Faction>());
            }
            RedoStack = new Stack<string>();
            LastMoveTime = DateTime.Now;
            LogEntityList = new List<LogEntity>();



        }

        /// <summary>
        /// 사용자 정보 설정
        /// </summary>
        public void SetUserInfo()
        {
            //用户列表，配置信息
            if (this.UserGameModels == null)
            {
                this.UserGameModels = new List<UserGameModel>();
                foreach (string item in this.Username)
                {
                    if (item.Equals(""))
                    {
                        continue;
                    }
                    this.UserGameModels.Add(new UserGameModel()
                    {
                        username = item,
                        isTishi = true,
                        paygrade = this.dbContext?.Users.SingleOrDefault(user => user.UserName == item)?.paygrade,
                    });
                }
            }
        }

        public bool ProcessSyntax(string user, string syntax, out string log)
        {
            /////此处为打版本标记之处
            //if (version == 0)
            //{
            //    //version = 2;
            //    //地图变更，版本改为3
            //    version = 3;
            //}
            log = string.Empty;
            syntax = syntax.ToLower();
            bool ret;
            switch (GameStatus.stage)
            {
                //지도 단계
                case Stage.RANDOMSETUP:
                    return ProcessSyntaxRandomSetup(syntax, ref log);
                //지도 회전
                case Stage.MAPROTATE:
                    return ProcessSyntaxMapRotate(syntax, ref log);
                case Stage.FACTIONSELECTION:
                    return ProcessSyntaxFactionSelect(user, syntax, ref log);
                case Stage.INITIALMINES:
                    ret = ProcessSyntaxIntialMines(syntax, ref log);
                    if (ret)
                    {
                        if (FactionList.All(x => x.FinishIntialMines()))
                        {
                            ChangeGameStatus(Stage.SELECTROUNDBOOSTER);
                            GameStatus.SetPlayerIndexLast();
                            return ret;
                        }
                        if (GameStatus.NextPlayerForIntial())
                        {
                            if (FactionList.Exists(x => x is Xenos && !x.FinishIntialMines()))
                            {
                                GameStatus.PlayerIndex = FactionList.FindIndex(x => x is Xenos);
                            }
                            else if (FactionList.Exists(x => x is Hive && !x.FinishIntialMines()))
                            {
                                GameStatus.PlayerIndex = FactionList.FindIndex(x => x is Hive);
                            }
                        }
                    }
                    return ret;
                case Stage.SELECTROUNDBOOSTER:
                    ret = ProcessSyntaxRoundBoosterSelect(syntax, ref log);
                    if (ret)
                    {
                        GameStatus.NextPlayerReverse();
                    }
                    ///所有人都选完RBT了
                    if (ret && GameStatus.PlayerIndex + 1 == GameStatus.PlayerNumber)
                    {
                        NewRound();
                    }
                    return ret;
                case Stage.ROUNDSTART:
                    ///吸能量
                    if (GameSyntax.leechPowerRegex.IsMatch(syntax))
                    {
                        ret = ProcessSyntaxLeechPower(syntax, ref log);
                        return ret;
                    }
                    else
                    {
                        //结束回合
                        ret = ProcessSyntaxCommand(syntax, ref log);
                        //低版本检测PASS到下一位玩家
                        if (version < 4)
                        {
                            if (ret && GameStatus.IsAllPass())
                            {
                                if (FactionList.All(x => x.LeechPowerQueue.Count == 0))
                                {
                                    FactionList = FactionNextTurnList;
                                    FactionNextTurnList = new List<Faction>();
                                    NewRound();
                                }
                                else
                                {
                                    GameStatus.stage = Stage.ROUNDWAITLEECHPOWER;
                                }
                            }
                            else if (ret)
                            {
                                //低版本直接到下一位玩家
                                GameStatus.NextPlayer(this.FactionList);
                            }
                        }


                        return ret;
                    }
                ///에너지 흡수 만 처리
                case Stage.ROUNDWAITLEECHPOWER:
                    if (GameSyntax.leechPowerRegex.IsMatch(syntax))
                    {
                        ret = ProcessSyntaxLeechPower(syntax, ref log);
                        if (ret && FactionList.All(x => x.LeechPowerQueue.Count == 0))
                        {
                            FactionList = FactionNextTurnList;
                            FactionNextTurnList = new List<Faction>();
                            NewRound();
                        }
                        return ret;
                    }
                    return false;
                //가이아 스테이지
                case Stage.ROUNDGAIAPHASE:
                    //탑에 검은 별을 넣으면 에너지를 흡수해야합니다.
                    if (GameSyntax.leechPowerRegex.IsMatch(syntax))
                    {
                        ret = ProcessSyntaxLeechPower(syntax, ref log);
                        if (ret && FactionList.All(x => x.LeechPowerQueue.Count == 0))
                        {
                            FactionList = FactionNextTurnList;
                            FactionNextTurnList = new List<Faction>();
                            NewRound();
                        }
                        return ret;
                    }
                    else
                    {
                        ret = ProcessSyntaxGaiaPhase(syntax, ref log);
                        if (ret)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }

                //소득 단계
                case Stage.ROUNDINCOME:

                    ret = ProcessSyntaxRoundIncomePhase(syntax, ref log);
                    if (ret)
                    {
                        IncomePhaseNextPlayer();
                    }
                    else
                    {
                        return false;
                    }
                    return true;
                default:
                    return false;
            }
        }

        private bool ProcessSyntaxRoundIncomePhase(string syntax, ref string log)
        {
            log = string.Empty;
            if (!ValidateSyntaxCommand(syntax, ref log, out string commmand, out Faction faction))
            {
                return false;
            }
            if (!GameSpecialSyntax.PowerPreview.IsMatch(commmand))
            {
                log = "语法错误";
                return false;
            }
            var match = GameSpecialSyntax.PowerPreview.Match(commmand);
            var p1 = match.Groups[1].Value.ParseToInt(0);
            var p2 = match.Groups[2].Value.ParseToInt(0);
            var p3 = match.Groups[3].Value.ParseToInt(0);
            var pr = faction.PowerPreview.FindIndex(x => x.Item1 == p1 && x.Item2 == p2 && x.Item3 == p3);
            if (pr!=-1)
            {
                faction.SetPowerPreview(pr);
            }
            else
            {
                log = "不能变为此种能量分配";
                return false;
            }
            return true;
        }

        private bool ProcessSyntaxGaiaPhase(string syntax, ref string log)
        {


            if (!ValidateSyntaxCommand(syntax, ref log, out string commmand, out Faction faction))
            {
                return false;
            }
            if (GameSpecialSyntax.PassRegex.IsMatch(commmand))
            {
                GaiaNextPlayer();
                return true;
            }

            var commandList = commmand.Split('.').Where(x => !string.IsNullOrEmpty(x));
            if (faction is Itar)
            {
                var itar = faction as Itar;


                if (!commandList.ToList().TrueForAll(x => GameFreeSyntax.getTechTilesRegex.IsMatch(x)
                || GameFreeSyntax.ReturnTechTilesRegex.IsMatch(x)
                || GameFreeSyntax.advTechRegex2.IsMatch(x)
                || GameFreeSyntax.NoAdvanceTechTrack.IsMatch(x)
                || GameFreeSyntax.PlanetRegex.IsMatch(x))//放置黑星
                )
                {
                    log = "只支持拿板子行动";
                    return false;
                }
                if (commandList.ToList().Sum(x => GameFreeSyntax.getTechTilesRegex.IsMatch(x) == true ? 1 : 0) != 1)
                {
                    log = "语句中必须包含且只包含一次拿板子行动";
                    return false;
                }
                if (faction.PowerTokenGaia < GameConstNumber.ItarGaiaGetTechTileCost)
                {
                    log = "盖亚区至少有四点能量豆";
                    return false;
                }
                itar.SpecialGetTechTile();
                var ret = ProcessCommandWithBackup(commandList.ToArray(), faction, out log);
                faction.ResetUnfinishAction();


                if (faction.PowerTokenGaia < GameConstNumber.ItarGaiaGetTechTileCost)
                {
                    GaiaNextPlayer();
                }
                return ret;
            }
            else if (faction is Terraner)
            {
                var terraner = faction as Terraner;
                //只支持convert行动
                if (commandList.Count() != 1)
                {
                    log = "一条语句进行一次转换";
                    return false;
                }
                if (!commandList.ToList().TrueForAll(x => GameFreeSyntax.ConvertRegex.IsMatch(x)))
                {
                    log = "只支持能量转换行动";
                    return false;
                }
                var str = commandList.First();
                var match = GameFreeSyntax.ConvertRegex.Match(str);
                var RFNum = match.Groups[1].Value.ParseToInt(0);
                var RFKind = match.Groups[2].Value;
                var RTNum = match.Groups[6].Value.ParseToInt(0);
                var RTKind = match.Groups[7].Value;
                var ret = terraner.ConvertGaiaPowerToAnother(RFNum, RFKind, RTNum, RTKind, out log);
                if (terraner.IsExitUnfinishFreeAction(out log))
                {
                    return false;
                }

                foreach (var item in faction.ActionQueue)
                {
                    item.Invoke();
                }
                faction.ResetUnfinishAction();


                if (faction.PowerTokenGaia == 0)
                {
                    GaiaNextPlayer();
                }
                return ret;
            }
            else
            {
                throw new Exception("其他种族暂时不支持Gaia阶段行动");
            }
        }

        /// <summary>
        /// 새로운 라운드
        /// </summary>
        public void NewRound()
        {


            //游戏结束
            if (GameStatus.RoundCount == GameConstNumber.GameRoundCount)
            {
                CalGameEndScore();
                ChangeGameStatus(Stage.GAMEEND);
            }
            else
            {
                //开始新回合
                ChangeGameStatus(Stage.ROUNDINCOME);


                FactionList.ForEach(x => x.CalIncome());
                FactionList.ForEach(x =>
                {
                    x.BuildPowerPreview();
                    if (x.PowerPreview.Count != 0)
                    {
                        GameStatus.IncomePhaseIndexQueue.Enqueue(FactionList.IndexOf(x));
                    }
                });
                GameStatus.NewRoundReset(this);
                IncomePhaseNextPlayer();

                

                return;
            }

            //保存结果到数据库
            DbGameSave.SaveGameToDb(this.dbContext,this);
        }
        /// <summary>
        /// 收入阶段
        /// </summary>
        public void IncomePhaseNextPlayer()
        {


            if (GameStatus.IncomePhaseIndexQueue.Count==0)
            {
                ChangeGameStatus(Stage.ROUNDGAIAPHASE);
                var spFaction = FactionList.FindAll(x => x is Itar || x is Terraner);
                foreach (var item in spFaction)
                {
                    if (item is Itar && item.PowerTokenGaia >= GameConstNumber.ItarGaiaGetTechTileCost && item.StrongHold == null)
                    {
                        GameStatus.GaiaPlayerIndexQueue.Enqueue(FactionList.IndexOf(item));
                    }
                    else if (item is Terraner && item.PowerTokenGaia > 0 && item.StrongHold == null)
                    {
                        GameStatus.GaiaPlayerIndexQueue.Enqueue(FactionList.IndexOf(item));
                    }
                }
                GaiaNextPlayer();
            }
            else
            {
                GameStatus.PlayerIndex = GameStatus.IncomePhaseIndexQueue.Dequeue();
                //如果当前玩家已经drop，跳过其收入阶段
                if (this.FactionList[GameStatus.PlayerIndex].dropType > 0)
                {
                    IncomePhaseNextPlayer();
                }
            }
        }

        public void GaiaNextPlayer()
        {
        
            if (GameStatus.GaiaPlayerIndexQueue.Count == 0)
            {
                GaiaPhase();
            }
            else
            {
                GameStatus.PlayerIndex = GameStatus.GaiaPlayerIndexQueue.Dequeue();
            }

            //如果当前玩家已经drop
            if (this.FactionList[GameStatus.PlayerIndex].dropType > 0)
            {
                GaiaNextPlayer();
            }
        }

        private void CalGameEndScore()
        {
            FactionList.ForEach(x => x.FinalEndScore = 0);
            FSTList.ForEach(x => x.InvokeGameTileAction(FactionList));
            foreach (var item in FactionList)
            {

                item.Score += item.GetFinalEndScore(); 
                
            }
        }

        private void GaiaPhase()
        {
            foreach (var item in Map.HexArray)
            {
                if (item?.Building is GaiaBuilding)
                {
                    item.TFTerrain = Terrain.Green;
                }
            }

            FactionList.ForEach(x => x.GaiaPhaseIncome());

            //重置玩家
            GameStatus.SetPlayerIndex(this);

            FactionList.ForEach(x => x.GameTileList.Where(y=>!(y is AllianceTile)).ToList().ForEach(y => y.IsUsed = false));
            MapActionMrg.Reset();
            FactionList.ForEach(x => x.ResetNewRound());
            ChangeGameStatus(Stage.ROUNDSTART);
        }

        private bool ProcessSyntaxLeechPower(string syntax, ref string log)
        {
            if (!(ValidateSyntaxCommandForLeech(syntax, ref log, out string command, out Faction faction)))
            {
                return false;
            }
            var match = GameSyntax.leechPowerRegex.Match(syntax);
            var isLeech = match.Groups[1].Value.Equals(GameSyntax.leech);
            var power = match.Groups[2].Value.ParseToInt();
            var factionFromStr = match.Groups[3].Value;
            Enum.TryParse(factionFromStr, true, out FactionName factionFrom);
            if (faction is Taklons && faction.StrongHold == null)
            {
                var pwtfirst = match.Groups[4].Value.Trim();
                if (string.IsNullOrEmpty(pwtfirst))
                {
                    return false;
                }
                if (pwtfirst.Equals("pwt"))
                {
                    if (isLeech)//吸收
                    {
                        faction.PowerToken1++;
                    }
                    try
                    {
                        if (!faction.LeechPower(power, factionFrom, isLeech))
                        {
                            faction.PowerToken1--;
                            return false;
                        }
                    }
                    catch
                    {
                        faction.PowerToken1--;
                        return false;
                    }
                }
                else
                {
                    if (faction.LeechPower(power, factionFrom, isLeech))
                    {
                        //如果吸收了能力才加token
                        if (isLeech)
                        {
                            faction.PowerToken1++;
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                faction.LeechPower(power, factionFrom, isLeech);
                return true;
            }
        }

        private bool ProcessSyntaxCommand(string syntax, ref string log)
        {
            if (!(ValidateSyntaxCommand(syntax, ref log, out string command, out Faction faction)))
            {
                return false;
            }
            
            var commandList = command.Split('.').Where(x=>!string.IsNullOrEmpty(x));
            //非免费行动只能执行一个
            var NoneFreeActionCount=commandList.Sum(y => GameFreeSyntax.GetRegexList().Exists(x => x.IsMatch(y)) ? 0 : 1);
            bool isflag = commandList.ToList().Exists(x => GameFreeSyntax.advTechRegex2.IsMatch(x));
            //升级科技
            if (isflag&& NoneFreeActionCount == 0) { faction.IsSingleAdvTechTrack = true; }
            //是否包含主要行动
            bool isExitsAction = (NoneFreeActionCount==1 || isflag);
            //判断是否唯一主要行动
            if (isExitsAction && this.version>3){
                //是否执行过主要行动
                if (faction.IsActionBurn)
                {
                    log = "已经执行过主要行动";
                    return false;
                }
                //临时设置已经执行主要行动
                faction.IsActionBurn = true;

            }
            //主要行动次数过多
            else if (NoneFreeActionCount > 1)
            {
                log = "能且只能执行一个主要行动";
                return false;
            }
            //判断是否执行主要行动
//            if (NoneFreeActionCount !=1)
//            {
//                log = "主要行动只能执行一次";
//                return false;
//            }

            //能接建造行动的action也只能用一个
            if (commandList.Sum(y => GameFreeSyntax.actionRegex.IsMatch(y) ? 1 : 0) > 1)
            {
                log = "各种特殊Act一回合只能执行一次";
                return false;
            }

            var ret=ProcessCommandWithBackup(commandList.ToArray(),faction,out log);
            faction.ResetUnfinishAction();
            //如果执行没有成功
            if (isExitsAction && !ret)
            {
                faction.IsActionBurn = false;
            }
            return ret;
        }
        private bool ProcessCommandWithBackup(string[] commandList, Faction faction, out string log)
        {
            var ret = ProcessSingleCommand(commandList, faction, out log);
            if (!ret)
            {
                //如果命令失败了回滚所有代码
                foreach (var item in faction.UnDoActionQueue)
                {
                    item.Invoke();
                }
            }
            return ret;
        }
        private bool ProcessSingleCommand(string[] commandList,Faction faction,out string log)
        {
            log = string.Empty;
            //faction.ResetUnfinishAction();

            foreach (var itemT in commandList)
            {
                var item = itemT.Trim();
                if (GameSyntax.upgradeRegex.IsMatch(item))
                {
                    var match = GameSyntax.upgradeRegex.Match(item);
                    var pos = match.Groups[1].Value;
                    var buildStr = match.Groups[2].Value;
                    ConvertPosToRowCol(pos, out int row, out int col);
                    if (!faction.UpgradeBuilding(Map, row, col, buildStr, out log))
                    {
                        return false;
                    }
                }
                else if (GameSyntax.downgradeRegex.IsMatch(item))
                {
                    if (!(faction is Firaks))
                    {
                        log = "Firaks专用指令";
                        return false;
                    }
                    if (faction.FactionSpecialAbility <= 0)
                    {
                        log = "action fir才能使用DownGrade指令";
                        return false;
                    }
                    var match = GameSyntax.downgradeRegex.Match(item);
                    var pos = match.Groups[1].Value;
                    ConvertPosToRowCol(pos, out int row, out int col);
                    if (!(faction as Firaks).DowngradeBuilding(row, col, out log))
                    {
                        return false;
                    }
                }
                else if (GameSyntax.buildRegex.IsMatch(item))
                {
                    var match = GameSyntax.buildRegex.Match(item);
                    var pos = match.Groups[1].Value;
                    ConvertPosToRowCol(pos, out int row, out int col);
                    if (!faction.BuildMine(Map, row, col, out log))
                    {
                        return false;
                    }
                }
                else if (GameSyntax.gaiaRegex.IsMatch(item))
                {
                    var match = GameSyntax.gaiaRegex.Match(item);
                    var pos = match.Groups[1].Value;
                    ConvertPosToRowCol(pos, out int row, out int col);
                    if (!faction.BuildGaia(Map, row, col, out log))
                    {
                        return false;
                    }
                }
                else if (GameFreeSyntax.getTechTilesRegex.IsMatch(item))
                {
                    if (!GetTechTile(item, faction, out log))
                    {
                        return false;
                    }
                }
                else if (GameFreeSyntax.advTechRegex2.IsMatch(item))
                {

                    string tech;
                    if (faction.TechTracAdv <= 0 && !faction.IsSingleAdvTechTrack)
                    {
                        log = "没有拿到科技板";
                        return false;
                    }

                    if (faction.Knowledge < 4 && faction.IsSingleAdvTechTrack)
                    {
                        if (!(faction is MadAndroid && (faction as MadAndroid).IsMadAndroidAbilityUsed == false))
                        {
                            log = "科技不足四点";
                            return false;
                        }
                    }


                    var match = GameFreeSyntax.advTechRegex2.Match(item);
                    tech = match.Groups[1].Value;
                    if (faction.IsIncreateTechValide(tech, out log, true))
                    {
                        if ("ship".Equals(tech) && faction.ShipLevel == 4)
                        {
                            faction.PlanetGet++;
                        }
                        Action queue = () =>
                        {
                            faction.IncreaseTech(tech);
                            faction.Knowledge = faction.Knowledge;
                        };
                        faction.ActionQueue.Enqueue(queue);
                        if (faction.TechTracAdv > 0)
                        {
                            faction.TechTracAdv--;
                        }
                        else
                        {
                            faction.TempKnowledge -= 4;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (GameFreeSyntax.NoAdvanceTechTrack.IsMatch(item))
                {
                    faction.TechTracAdv--;
                    faction.IsNoAdvTechTrack = true;
                    if (faction.PlanetAlready)
                    {
                        log = "建立死星则不允许不升级科技";
                        return false;
                    }
                    else if (faction.PlanetGet == 1)
                    {
                        faction.PlanetGet--;
                    }
                }
                else if (GameSyntax.passRegex.IsMatch(item))
                {
                    var match = GameSyntax.passRegex.Match(item);
                    var rbtStr = match.Groups[1].Value;
                    if (!ProcessGetRoundBooster(rbtStr, faction, out log))
                    {
                        return false;
                    }
                    //立即执行
                    FactionNextTurnList.Add(faction);
                    GameStatus.SetPassPlayerIndex(FactionList.IndexOf(faction));
                    //                    Action action = () =>
                    //                    {
                    //                        FactionNextTurnList.Add(faction);
                    //                        GameStatus.SetPassPlayerIndex(FactionList.IndexOf(faction));
                    //                    };
                    //                    faction.ActionQueue.Enqueue(action);
                }
                else if (GameSyntax.actionRegex.IsMatch(item))
                {
                    var match = GameSyntax.actionRegex.Match(item);
                    var actionStr = match.Groups[1].Value;
                    if (faction.PredicateAction(actionStr, out log))
                    {
                        faction.DoAction(actionStr, false);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (GameFreeSyntax.actionRegex.IsMatch(item))
                {
                    var match = GameFreeSyntax.actionRegex.Match(item);
                    var actionStr = match.Groups[1].Value;
                    if (faction.PredicateAction(actionStr, out log))
                    {
                        faction.DoAction(actionStr, true);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (GameSyntax.forgingAllianceV2.IsMatch(item))
                {
                    var posStrList = item.Substring(GameSyntax.alliance.Length + 1).Split(',');
                    List<Tuple<int, int>> list = new List<Tuple<int, int>>();
                    foreach (var pos in posStrList)
                    {
                        ConvertPosToRowCol(pos, out int row, out int col);
                        list.Add(new Tuple<int, int>(row, col));
                    }
                    if (faction.ForgingAllianceCheck(list, out log))
                    {
                        faction.ForgingAllianceGetTiles(list);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (GameSyntax.forgingAlliance.IsMatch(item))
                {
                    if (!(faction is Hive))
                    {
                        log = "不加地点出城是蜂人专用语句";
                        return false;
                    }
                    if (faction.ForgingAllianceCheck(null, out log))
                    {
                        faction.ForgingAllianceGetTiles(null);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (GameFreeSyntax.ALTRegex.IsMatch(item))
                {
                    var match = GameFreeSyntax.ALTRegex.Match(item);
                    var altStr = match.Groups[1].Value;
                    var alt = ALTList.Find(x => x.GetType().Name.Equals(altStr, StringComparison.OrdinalIgnoreCase));
                    if (alt == null)
                    {
                        log = string.Format("{0}板子不存在", alt);
                        return false;
                    }
                    if (!faction.GetAllianceTile(alt, out log))
                    {
                        return false;
                    }

                }
                else if (GameFreeSyntax.burningRegex.IsMatch(item))
                {
                    var match = GameFreeSyntax.burningRegex.Match(item);
                    var v = match.Groups[1].Value.ParseToInt();
                    if (!(faction.PowerToken2 >= v * 2))
                    {
                        log = item + "需要" + v * 2 + "能量";
                        return false;
                    }
                    faction.PowerBurnSpecialPreview(v);
                    faction.TempPowerToken2 -= v * 2;
                    faction.TempPowerToken3 += v;
                    Action action = () =>
                    {
                        faction.PowerBurnSpecialActual(v);
                        faction.PowerToken2 = faction.PowerToken2;
                        faction.PowerToken3 = faction.PowerToken3;
                        faction.TempPowerToken2 = 0;
                        faction.TempPowerToken3 = 0;
                    };
                    faction.ActionQueue.Enqueue(action);
                }
                else if (GameFreeSyntax.ConvertRegex.IsMatch(item))
                {
                    var match = GameFreeSyntax.ConvertRegex.Match(item);
                    var RFNum = match.Groups[1].Value.ParseToInt(0);
                    var RFKind = match.Groups[2].Value;
                    var RTNum = match.Groups[6].Value.ParseToInt(0);
                    var RTKind = match.Groups[7].Value;
                    int? RFNum2 = null;
                    string RFKind2 = null;
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                    {
                        RFNum2 = match.Groups[4].Value.ParseToInt(0);
                        RFKind2 = match.Groups[5].Value;
                    }
                    int? RTNum2 = null;
                    string RTKind2 = null;
                    if (!string.IsNullOrEmpty(match.Groups[8].Value))
                    {
                        RTNum2 = match.Groups[9].Value.ParseToInt(0);
                        RTKind2 = match.Groups[10].Value;
                    }
                    if (!faction.ConvertOneResourceToAnother(RFNum, RFKind, RTNum, RTKind, out log, RTNum2, RTKind2, RFNum2, RFKind2))
                    {
                        return false;
                    }
                }
                else if (GameFreeSyntax.ReturnTechTilesRegex.IsMatch(item))
                {
                    if (faction.TechReturn == 0)
                    {
                        log = "你不能退回科技版";
                        return false;
                    }
                    var techTileStr = item.Substring(1);
                    var tile = faction.GameTileList.Find(x => string.Compare(x.GetType().Name, techTileStr, true) == 0) as StandardTechnology;

                    if (tile == null)
                    {
                        log = "你没有此块STT版";
                        return false;
                    }

                    Action queue = () =>
                    {
                        //覆盖的版块，删除板块
                        faction.GameTileListCovered.Add(tile);
                        faction.RemoveGameTiles(tile);
                    };
                    faction.ActionQueue.Enqueue(queue);
                    faction.TechReturn--;
                }
                else if (GameFreeSyntax.PlanetRegex.IsMatch(item))
                {
                    faction.PlanetGet--;
                    faction.PlanetAlready = true;
                    var match = GameFreeSyntax.PlanetRegex.Match(item);
                    var pos = match.Groups[1].Value;
                    ConvertPosToRowCol(pos, out int row, out int col);
                    if (!faction.BuildBlackPlanet(row, col, out log))
                    {
                        faction.blankMine = 0;
                        return false;
                    }
                    else
                    {
                        //成功放置黑星的话
                        faction.blankMine = 1;
                    }

                }
                else if (GameFreeSyntax.AllianceTileReGexRegex.IsMatch(item))
                {
                    if (faction.AllianceTileReGet <= 0)
                    {
                        log = "没有重新计分星盟板块的资格";
                        return false;
                    }
                    faction.AllianceTileReGet--;
                    var match = GameFreeSyntax.AllianceTileReGexRegex.Match(item);
                    var altStr = match.Groups[1].Value;
                    var alt = faction.GameTileGet(altStr) as AllianceTile;
                    if (alt == null)
                    {
                        log = string.Format("你并没有{0}星盟版", altStr);
                        return false;
                    }
                    Action action = () =>
                    {
                        alt.OneTimeAction(faction);
                    };
                    faction.ActionQueue.Enqueue(action);
                }
                else if (GameSyntax.swapRegex.IsMatch(item))
                {
                    if (!(faction is Ambas))
                    {
                        log = "只有Ambas能使用该SH能力";
                        return false;
                    }
                    var ambas = faction as Ambas;
                    if (faction.FactionSpecialAbility <= 0)
                    {
                        log = "action amb才能使用swap语句";
                        return false;
                    }
                    var match = GameSyntax.swapRegex.Match(item);
                    ConvertPosToRowCol(match.Groups[1].Value, out int row, out int col);
                    var pos1 = new Tuple<int, int>(row, col);
                    ConvertPosToRowCol(match.Groups[2].Value, out row, out col);
                    var pos2 = new Tuple<int, int>(row, col);
                    if (!ambas.ExcuteSHAbility(pos1, pos2, out log))
                    {
                        return false;
                    }
                }
                //结束自己的当前回合
                else if (GameFreeSyntax.PassRegexTurn.IsMatch(item))
                {
                    //执行过主要行动
                    if (faction.IsActionBurn)
                    {
                        //复位玩家执行主要行动
                        faction.IsActionBurn = false;
                        //回合结束
                        if (GameStatus.IsAllPass())
                        {
                            if (FactionList.All(x => x.LeechPowerQueue.Count == 0))
                            {
                                FactionList = FactionNextTurnList;
                                FactionNextTurnList = new List<Faction>();
                                NewRound();
                            }
                            else
                            {
                                GameStatus.stage = Stage.ROUNDWAITLEECHPOWER;
                            }
                        }
                        else
                        {
                            //到下一位玩家
                            GameStatus.NextPlayer(this.FactionList);
                        }
                    }
                    else
                    {
                        log = "还没有执行主要行动";
                        return false;
                    }



                }
                //重置当论操作
                else if (GameFreeSyntax.ResetRegexTurn.IsMatch(item))
                {
                    //当前用户
                    var myUser = this.UserGameModels.Find(user => user.username == GetCurrentUserName());

                    if (myUser != null)
                    {
                        //会员回退
                        if (item.Equals("reset turn2"))
                        {

                        }
                        else
                        {
                            if (myUser.resetNumber > 0)
                            {
                                //重置参数-1
                                myUser.resetNumber--;

                                var syntaxList = this.UserActionLog.Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                //删除日志
                                for (int i = syntaxList.Count - 1; i > 0; i--)
                                {
                                    string str = syntaxList[i];
                                    var list = str.Split(':');
                                    //隔开
                                    if (list.Length == 2)
                                    {
                                        //当前种族操作
                                        if (list[0] == faction.FactionName.ToString())
                                        {
                                            syntaxList.RemoveAt(i);
                                        }
                                        //不是当前种族
                                        else
                                        {
                                            //跳过吸收能量
                                            if (GameSyntax.leechPowerRegex.IsMatch(list[1]) || GameSyntax.downgradeRegex.IsMatch(list[1]))
                                            {

                                            }
                                            //如果是pass ，终止
                                            else if (GameFreeSyntax.PassRegexTurn.IsMatch(list[1]))
                                            {
                                                break;
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        syntaxList.RemoveAt(i);
                                    }
                                    //if (list[0])
                                }
                                this.UserActionLog = string.Join("\r\n", syntaxList);
                                //重置游戏
                                GameMgr.RestoreGame(this.GameName, this, isToDict: true);
                            }
                            else
                            {
                                log = "不能进行重置";
                                return false;
                            }
                        }

                    }
                }
                else if (GameFreeSyntax.DropFactionRegex.IsMatch(item))
                {
                    this.DropFaction();
                }
                else
                {
                    log = "语句还不支持";
                    return false;
                }
            }
            if (faction.IsExitUnfinishFreeAction(out log))
            {
                return false;
            }

            foreach (var item in faction.ActionQueue)
            {
                item.Invoke();
            }
            //检测q,o,c,k是否小于0
            if (faction.QICs < 0 || faction.Ore < 0 || faction.Credit < 0 || faction.Knowledge < 0 ||faction.PowerToken2<0 || faction.PowerToken3<0)
            {
                log = "资源不够";
                //重新执行全部操作，重新恢复游戏
                GameMgr.RestoreGame(this.GameName, this, true);
                return false;
            }

            return true;
        }
        /// <summary>
        /// 拿取科技版
        /// </summary>
        /// <param name="item"></param>
        /// <param name="faction"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private bool GetTechTile(string item,Faction faction,out string log)
        {
            log = string.Empty;
            var techTileStr = item.Substring(1);
            GameTiles tile;
            faction.TechTracAdv++;
            if (ATTList.Exists(x => string.Compare(x.GetType().Name, techTileStr, true) == 0))
            {
                tile = ATTList.Find(x => string.Compare(x.GetType().Name, techTileStr, true) == 0);
                if ((tile as AdavanceTechnology).isPicked)
                {
                    log = "板子已经被拿走了";
                    return false;
                }
                var index = ATTList.IndexOf(tile as AdavanceTechnology);
                var level = faction.GetTechLevelbyIndex(index);
                if (!(level == 4 || level == 5))
                {
                    log = "拿取该高级科技版对应科技等级不够";
                    return false;
                }
                if (!faction.GameTileList.Exists(x => x is AllianceTile && x.IsUsed == false))
                {
                    log = "没有没翻面的城邦";
                    return false;
                }
                faction.TechReturn++;
                faction.AllianceTileCost++;
            }
            else if (STT3List.Exists(x => string.Compare(x.GetType().Name, techTileStr, true) == 0))
            {
                tile = STT3List.Find(x => string.Compare(x.GetType().Name, techTileStr, true) == 0);
            }
            else if (STT6List.Exists(x => string.Compare(x.GetType().Name, techTileStr, true) == 0))
            {
                faction.TechTracAdv--;
                tile = STT6List.Find(x => string.Compare(x.GetType().Name, techTileStr, true) == 0);
                var index = (tile as StandardTechnology).Index.GetValueOrDefault();
                faction.LimitTechAdvance = Faction.ConvertTechIndexToStr(index);
                if (faction.IsIncreateTechValide(faction.LimitTechAdvance,out log))
                {
                    if ("ship".Equals(faction.LimitTechAdvance) && faction.ShipLevel == 4)
                    {
                        faction.PlanetGet++;
                    }
                }
            }
            else
            {
                log = string.Format("{0}这块板子不存在", techTileStr);
                return false;
            }
            if (faction.GameTileList.Exists(x => x.GetType().Name.Equals(tile.GetType().Name)))
            {
                log = string.Format("玩家已经获得该板块{0}", tile.GetType().Name);
                return false;
            }

            if (faction.GameTileListCovered.Exists(x => x.GetType().Name.Equals(tile.GetType().Name)))
            {
                log = string.Format("玩家曾经获得过该板块{0}", tile.GetType().Name);
                return false;
            }
            
            Action queue = () =>
            {
                if (ATTList.Exists(x => string.Compare(x.GetType().Name, techTileStr, true) == 0))
                {
                    (tile as AdavanceTechnology).isPicked = true;
                    faction.GameTileList.Find(x => x is AllianceTile && x.IsUsed == false).IsUsed = true;
                }
                if (STT6List.Exists(x => string.Compare(x.GetType().Name, techTileStr, true) == 0))
                {
                    var index = (tile as StandardTechnology).Index.GetValueOrDefault();
                    faction.LimitTechAdvance = Faction.ConvertTechIndexToStr(index);
                    if (faction.IsNoAdvTechTrack == false && faction.IsIncreateTechValide(faction.LimitTechAdvance,out string t))
                    {
                        faction.IncreaseTech(faction.LimitTechAdvance);
                    }
                    STT6List.Remove(tile as StandardTechnology);
                }
                else if (STT3List.Exists(x => string.Compare(x.GetType().Name, techTileStr, true) == 0))
                {
                    STT3List.Remove(tile as StandardTechnology);
                }
                faction.AddGameTiles(tile);
                //数据库保存拿版记录
                DbGameSave.SaveTTData(this,faction,techTileStr);

            };
            faction.ActionQueue.Enqueue(queue);
            faction.TechTilesGet--;
            return true;
        }

        private bool ProcessSyntaxRoundBoosterSelect(string syntax, ref string log)
        {
            if (!(ValidateSyntaxCommand(syntax, ref log, out string command, out Faction faction)))
            {
                return false;
            }
            ///处理SelectRBT命令
            if (!GameSyntax.RBTRegex.IsMatch(command))
            {
                log = "命令错误";
                return false;
            }
            var rbtStr = command.Substring(1);

            if(ProcessGetRoundBooster(rbtStr, faction, out log))
            {
                foreach (var item in faction.ActionQueue)
                {
                    item.Invoke();
                }
                faction.ResetUnfinishAction();
                return true;
            }
            else
            {
                faction.ResetUnfinishAction();
                return false;
            }
        }

        private bool ProcessGetRoundBooster(string rbtStr, Faction faction, out string log)
        {
            log = string.Empty;
            var rbt = RBTList.Find(x => x.GetType().Name.Equals(rbtStr, StringComparison.OrdinalIgnoreCase));
            if (rbt == null)
            {
                log = string.Format("{0}板子不存在", rbtStr);
                return false;
            }
            //立即执行
            faction.GameTileList.ForEach(y => faction.Score += y.GetTurnEndScore(faction));
            if (faction.GameTileList.Exists(x => x is RoundBooster))
            {
                var ret = faction.GameTileList.Find(x => x is RoundBooster) as RoundBooster;
                RBTList.Add(ret);
                faction.GameTileList.Remove(ret);
                ret.IsUsed = false; ;
                faction.PredicateActionList.Remove(ret.GetType().Name.ToLower());
                faction.ActionList.Remove(ret.GetType().Name.ToLower());
            }
            faction.AddGameTiles(rbt);
            RBTList.Remove(rbt);

//            Action action = () =>
//            {
//            //回合结束计分
//                faction.GameTileList.ForEach(y => faction.Score += y.GetTurnEndScore(faction));
//                if (faction.GameTileList.Exists(x => x is RoundBooster))
//                {
//                    var ret = faction.GameTileList.Find(x => x is RoundBooster) as RoundBooster;
//                    RBTList.Add(ret);
//                    faction.GameTileList.Remove(ret);
//                    ret.IsUsed = false; ;
//                    faction.PredicateActionList.Remove(ret.GetType().Name.ToLower());
//                    faction.ActionList.Remove(ret.GetType().Name.ToLower());
//                }
//                faction.AddGameTiles(rbt);
//                RBTList.Remove(rbt);
//            };
//            faction.ActionQueue.Enqueue(action);
            return true;
        }

        private bool ProcessSyntaxIntialMines(string syntax, ref string log)
        {
            if (!(ValidateSyntaxCommand(syntax, ref log, out string command, out Faction faction)))
            {
                return false;
            }
            ///处理Build命令
            if (!GameSyntax.buildRegex.IsMatch(command))
            {
                log = "命令错误";
                return false;
            }
            ///Build A2
            var position = command.Substring(GameSyntax.factionSelection.Length + 1);

            ConvertPosToRowCol(position, out int row, out int col);

            if (faction.BuildIntialMine(Map, row, col, out log))
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        private static void ConvertPosToRowCol(string position, out int row, out int col)
        {
            row = position.Substring(0, 1).ToCharArray().First() - 'a';
            col = position.Substring(1).ParseToInt(0);
        }
        private bool ValidateSyntaxCommand(string syntax, ref string log, out string command, out Faction faction)
        {
            if (!ValidateSyntaxCommandForLeech(syntax, ref log, out command, out faction))
            {
                return false;
            }

            if (!(FactionList[GameStatus.PlayerIndex] == faction))
            {
                log = string.Format("不是种族{0}行动轮,是{1}行动轮", faction.FactionName, FactionList[GameStatus.PlayerIndex].FactionName.ToString());
                return false;
            }

            if (faction.LeechPowerQueue.Count != 0 && !syntax.Contains("drop"))
            {
                log = "必须先执行吸取能量行动";
                return false;
            }
            return true;
        }
        private bool ValidateSyntaxCommandForLeech(string syntax, ref string log, out string command, out Faction faction)
        {
            command = string.Empty;
            faction = null;
            if (!GameSyntax.commandRegex.IsMatch(syntax))
            {
                log = "格式为 种族:命令";
                return false;
            }

            var factionName = syntax.Split(':').First();
            if (!Enum.TryParse(factionName, true, out FactionName result))
            {
                log = "FactionName is wrong";
                return false;
            }

            faction = FactionList.Find(x => x.FactionName == result);
            if (faction == null)
            {
                log = "FactionName doesn't exit";
                return false;
            }

            command = syntax.Split(':').Last();
            return true;
        }

        private bool ProcessSyntaxFactionSelect(string user,string syntax, ref string log)
        {
            log = string.Empty;
            if (GameSyntax.factionSelectionRegex.IsMatch(syntax))
            {
                var faction = syntax.Substring(GameSyntax.factionSelection.Length + 1);
                if (Enum.TryParse(faction, true, out FactionName result))
                {
                    if (FactionList.Exists(x => (int)x.FactionName / 2 == (int)result / 2))
                    {
                        log = "不能选择相同颜色的种族";
                        return false;
                    }

                    if (!FactionList.Exists(x => x.FactionName == result))
                    {
                        SetupFaction(user, result);
                        GameStatus.NextPlayer(this.FactionList);
                    }
                    else
                    {
                        log = "FactionName has been choosen!";
                        return false;
                    }
                    if (FactionList.Count == GameStatus.PlayerNumber)
                    {
                        ChangeGameStatus(Stage.INITIALMINES);
                        var i = FactionList.FindIndex(x => x is Hive);
                        if (i != -1)
                        {
                            GameStatus.SetPassPlayerIndex(i);
                        }

                        if (FactionList.FindIndex(x => x is Hive) == 0)
                        {
                            GameStatus.NextPlayerForIntial();
                        }
                    }
                    return true;
                }
                else
                {
                    log = "FactionName is wrong";
                }
            }
            return false;
        }
        /// <summary>
        /// 随机生产地图
        /// </summary>
        /// <param name="syntax"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private bool ProcessSyntaxRandomSetup(string syntax, ref string log)
        {
            log = string.Empty;
            if (GameSyntax.setupGameRegex.IsMatch(syntax))
            {
                var seed = syntax.Substring(GameSyntax.setupGame.Length).ParseToInt(0);
                GameStart(syntax, seed);
                //如果需要旋转地图，
                if (this.IsRotatoMap && (this.MapSelection==MapSelection.random2p|| this.MapSelection == MapSelection.random3p|| this.MapSelection == MapSelection.random4p|| this.MapSelection == MapSelection.randomall4p))
                {
                    ChangeGameStatus(Stage.MAPROTATE);
                }
                else//直接进入旋转种族
                {
                    ChangeGameStatus(Stage.FACTIONSELECTION);
                }
                return true;
            }else if (GameSyntax.setupMapRegex.IsMatch(syntax))
            {
                var match = GameSyntax.setupMapRegex.Match(syntax);
                var str = match.Groups[1].Value;
                MapSelection = (MapSelection)Enum.Parse(typeof(MapSelection), str, true);
                return true;
            }

            return false;
        }
        /// <summary>
        /// 旋转地图
        /// </summary>
        /// <param name="syntax"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private bool ProcessSyntaxMapRotate(string syntax, ref string log)
        {
            log = string.Empty;
            //结束旋转
            if (GameSpecialSyntax.PassRegex.IsMatch(syntax))
            {
                ChangeGameStatus(Stage.FACTIONSELECTION);
                return true;
            }
            //旋转
            else if (GameSyntax.setupMapRotateRegex.IsMatch(syntax))
            {
                var match = GameSyntax.setupMapRotateRegex.Match(syntax);
                //星域 方向 步数
                var str = match.Groups[1].Value;
                var fx = match.Groups[2].Value;
                var bs= Int32.Parse(match.Groups[3].Value);
                SpaceSector spaceSector = this.Map.ListSpaceSector.SingleOrDefault(item => item.Name == str);
                int x = spaceSector.CenterA;
                int y = spaceSector.CenterB;
                SpaceSector newSector= spaceSector;
                for (int i = 0; i < bs; i++)
                {
                    newSector = newSector?.Rotate(isClockwise:fx == "1",version:this.version);
                }
                if (newSector != null) this.Map.AddSpaceSector(x, y, newSector, null,this.version);
//                var seed = syntax.Substring(GameSyntax.setupGame.Length).ParseToInt(0);
//                GameStart(syntax, seed);
//                ChangeGameStatus(Stage.ROUNDSTART);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 数据库操作
        /// </summary>
        public ApplicationDbContext dbContext;

        public string syntax;//最后一条保存
        public void Syntax(string syntax, out string log, string user = "",ApplicationDbContext dbContext = null)
        {
            //dbContext = 
            if (dbContext != null)
            {
                //赋值
                this.dbContext = dbContext;
            }
            this.syntax = syntax;

            try
            {
                log = string.Empty;
                if (syntax.StartsWith("#"))
                    return;
                var currentFaction = GetCurrentFaction();
                var turnStart = currentFaction?.BackupResource();
                if (ProcessSyntax(user, syntax, out log))
                {
                    UserActionLog += syntax.AddEnter();
                    UserActionLog += m_TailLog;
                    m_TailLog = string.Empty;
                    currentFaction?.GetResouceChange(turnStart);
                    LogEntityList.Add(new LogEntity()
                    {
                        FactionName = currentFaction?.FactionName,
                        ChineseName = currentFaction?.ChineseName,
                        Row = LogEntityList.Count + 1,
                        Syntax = syntax,
                        ResouceChange = turnStart,
                        ResouceEnd = currentFaction?.BackupResource()
                    });
                    if (currentFaction != null)
                    {
                        currentFaction.ClockPerid += DateTime.Now - LastMoveTime.GetValueOrDefault();
                    }
                }
                else
                {
                    UserActionLog += "##" + DateTime.Now.ToString() + "#" + syntax.AddEnter();
                }
            }

            catch (Exception ex)
            {
                LastErrorLog = ex.ToString();
                UserActionLog += "##!!##" + DateTime.Now.ToString() + "#" + syntax.AddEnter();
                log = "引起程序异常,将本局名字报告给TOTO以方便排查问题";
                return;
            }
            finally
            {
                LastMoveTime = DateTime.Now;
            }
        }

        private Faction GetCurrentFaction()
        {
            if (FactionList.Count >= GameStatus.PlayerIndex + 1)
            {
                return FactionList[GameStatus.PlayerIndex];
            }
            else
            {
                return null;
            }
        }

        private void SetupFaction(string user,FactionName faction)
        {
            switch (faction)
            {
                case FactionName.Terraner:
                    FactionList.Add(new Terraner(this));
                    break;
                case FactionName.Taklons:
                    FactionList.Add(new Taklons(this));
                    break;
                case FactionName.MadAndroid:
                    FactionList.Add(new MadAndroid(this));
                    break;
                case FactionName.Geoden:
                    FactionList.Add(new Geoden(this));
                    break;
                case FactionName.Ambas:
                    FactionList.Add(new Ambas(this));
                    break;
                case FactionName.Lantida:
                    FactionList.Add(new Lantida(this));
                    break;
                case FactionName.Firaks:
                    FactionList.Add(new Firaks(this));
                    break;
                case FactionName.BalTak:
                    FactionList.Add(new BalTak(this));
                    break;
                case FactionName.Hive:
                    FactionList.Add(new Hive(this));
                    break;
                case FactionName.HadschHalla:
                    FactionList.Add(new HadschHalla(this));
                    break;
                case FactionName.Itar:
                    FactionList.Add(new Itar(this));
                    break;
                case FactionName.Nevla:
                    FactionList.Add(new Nevla(this));
                    break;
                case FactionName.Gleen:
                    FactionList.Add(new Gleen(this));
                    break;
                case FactionName.Xenos:
                    FactionList.Add(new Xenos(this));
                    break;
                default:
                    break;
            };
            if (string.IsNullOrEmpty(user))
            {
                user = Username[FactionList.Count - 1];
            }
            UserDic[user].Add(FactionList.Last());
            FactionList.Last().UserName = user;
            //赋值usermodel
            FactionList.Last().UserGameModel = this.UserGameModels?.Find(userGame => userGame.username == user);
        }

        private void GameStart(string syntax, int i = 0)
        {
            Seed = i == 0 ? RandomInstance.Next(int.MaxValue) : i;
            var random = new Random(Seed);
            //实例化地图
            MapMgr mapMgr=new MapMgr(this);
            if (MapSelection == MapSelection.fix2p)
            {
                Map = mapMgr.Get2PFixedMap();
            }
            else if (MapSelection == MapSelection.random2p)
            {
                Map = mapMgr.Get2PRandomMap(random);
            }
            else if (MapSelection == MapSelection.fix3p)
            {
                Map = mapMgr.Get3PFixedMap();
            }
            else if (MapSelection == MapSelection.random3p)
            {
                Map = mapMgr.Get3PRandomMap(random);
            }
            else if (MapSelection == MapSelection.fix4p)
            {
                Map = mapMgr.Get4PFixedMap();
            }
            else if (MapSelection == MapSelection.random4p)
            {
                Map = mapMgr.Get4PRandomMap(random);
            }
            else if (MapSelection == MapSelection.randomall4p)
            {
                Map = mapMgr.Get4PAllRandomMap(random);
            }
            else
            {
                Map = mapMgr.Get4PFixedMap();
            }

            ATTList = ATTMgr.GetRandomList(6, random);
            STT6List = STTMgr.GetRandomList(6, random);
            STT3List = (from items in STTMgr.GetOtherList(STT6List) orderby items.GetType().Name.Remove(0, 3).ParseToInt(-1) select items).ToList();
            if (version == 1)
            {
                RSTList = RSTMgr.GetRandomList(6, random);
            }
            else if(version == 2)
            {
                RSTList = RSTMgr2.GetRandomList(6, random);
            }
            else
            {
                RSTList = RSTMgr2.GetRandomList(6, random);
            }

            FSTList = FSTMgr.GetRandomList(2, random);
            RBTList = (from items in RBTMgr.GetRandomList(GameStatus.PlayerNumber + 3, random) orderby items.GetType().Name.Remove(0, 3).ParseToInt(-1) select items).ToList();
            ALTList = ALTMgr.GetList();
            AllianceTileForTransForm = ALTList.RandomRemove(random);
        }
        /// <summary>
        /// 수정 단계
        /// </summary>
        /// <param name="stage"></param>
        private void ChangeGameStatus(Stage stage)
        {
            m_TailLog += "#" + stage.ToString().AddEnter();
            GameStatus.stage = stage;

        }
        public void SetLeechPowerQueue(FactionName factionName,int row,int col)
        {
            foreach(var item in FactionList.Where(x => !x.FactionName.Equals(factionName)))
            {
                //drop玩家不吸收能量
                if (item.dropType > 0)
                {
                    continue;
                }
                else
                {
                    var power = Map.CalHighestPowerBuilding(row, col, item);

                    if (power != 0)
                    {
                        //如果是第六回合并且PASS的玩家只需要吸收1魔力
                        if (this.GameStatus.RoundCount == 6 && this.FactionNextTurnList.Contains(item))
                        {
                            //如果吸收1魔力
                            if (power == 1)
                            {
                                item.PowerIncrease(1);
                            }
                            //或者最多吸收1魔力
                            else if (item.PowerToken1 == 0 && item.PowerToken2 == 1)
                            {
                                item.PowerIncrease(1);
                            }
                            continue;
                        }

                        item.LeechPowerQueue.Add(new Tuple<int, FactionName>(power, factionName));
                    }
                }
            }
        }
        public string GetLastMovePreriod()
        {
            if (!LastMoveTime.HasValue)
            {
                return "Unknown";
            }
            var t = LastMoveTime.GetValueOrDefault();
            if ((DateTime.Now - t).Days > 0)
            {
                return ((DateTime.Now - t).Days) + "Day";
            }
            if ((DateTime.Now - t).Hours > 0)
            {
                return ((DateTime.Now - t).Hours) + "Hours";
            }
            if ((DateTime.Now - t).Minutes > 0)
            {
                return ((DateTime.Now - t).Minutes) + "Minutes";
            }
            if ((DateTime.Now - t).Seconds > 0)
            {
                return ((DateTime.Now - t).Seconds) + "Seconds";
            }
            return "Unknown";
        }

        public string GetCurrentUserName()
        {
            //旋转地图

            if (GameStatus.stage == Stage.MAPROTATE)
            {
                return this.Username[this.UserCount - 1];
            }
            else if (GameStatus.stage == Stage.FACTIONSELECTION)
            {
                return Username[GameStatus.PlayerIndex];
            }
            else if (GameStatus.stage == Stage.ROUNDWAITLEECHPOWER || GameStatus.stage == Stage.GAMEEND)
            {
                return string.Empty;
            }
            else if (FactionList.Count <= GameStatus.PlayerIndex)
            {
                return string.Empty;
            }
            else if (GameStatus!=null && GameStatus.PlayerIndex < 0)
            {
                return string.Empty;
            }
            else if (UserDic.Where(x => x.Value.Contains(FactionList[GameStatus.PlayerIndex])).Any())
            {
                return UserDic.Where(x => x.Value.Contains(FactionList[GameStatus.PlayerIndex])).First().Key;
            }
            else
            {
                return string.Empty;
            }
        }
        /// <summary>
        /// drop种族
        /// </summary>
        private void DropFaction()
        {
            string userName = GetCurrentUserName();
            switch (GameStatus.stage)
            {
                case Stage.ROUNDSTART:
                {
                    Faction faction = FactionList.SingleOrDefault((Faction fac) => fac.UserName == userName);
                    faction.dropType = 1;
                    //如果需要吸收魔力，清空
                    if (faction.LeechPowerQueue != null && faction.LeechPowerQueue.Count > 0)
                    {
                        faction.LeechPowerQueue.Clear();
                    }
                    FactionNextTurnList.Add(faction);
                    GameStatus.SetPassPlayerIndex(FactionList.IndexOf(faction));
                    if (GameStatus.IsAllPass())
                    {
                        if (FactionList.All((Faction x) => x.LeechPowerQueue.Count == 0))
                        {
                            FactionList = FactionNextTurnList;
                            FactionNextTurnList = new List<Faction>();
                            NewRound();
                        }
                        else
                        {
                            GameStatus.stage = Stage.ROUNDWAITLEECHPOWER;
                        }
                    }
                    else
                    {
                        GameStatus.NextPlayer(FactionList);
                    }
                    break;
                }
                case Stage.ROUNDGAIAPHASE:
                    GaiaNextPlayer();
                    break;
                case Stage.ROUNDINCOME:
                    IncomePhaseNextPlayer();
                    break;
            }
            LastMoveTime = DateTime.Now;
        }

        /// <summary>
        /// 游戏名称
        /// </summary>

        [JsonProperty]
        public string GameName { get; set; }

        /// <summary>
        /// 实例化四个玩家
        /// </summary>
        public List<Faction> FactionList { set; get; }
        public List<Faction> FactionNextTurnList { set; get; }
#region 存档需要save的内容
        [JsonProperty]
        /// <summary>
        /// 在游戏开始的时候实例化一个Map
        /// </summary>
        public Map Map { set; get; }
        /// <summary>
        /// 游戏的一些公共状态包括现在第几轮轮到哪个玩家行动等等
        /// </summary>
        public GameStatus GameStatus { set; get; }
        /// <summary>
        /// StandardTechList
        /// </summary>
        public List<StandardTechnology> STT6List { set; get; }
        /// <summary>
        /// StandardTechListWithNoKnowledge
        /// </summary>
        public List<StandardTechnology> STT3List { set; get; }
        /// <summary>
        /// AdvanceTechList
        /// </summary>
        public List<AdavanceTechnology> ATTList { set; get; }
        /// <summary>
        /// RoundScoringList
        /// </summary>
        public List<RoundScoring> RSTList { set; get; }
        /// <summary>
        /// FinalScoringList
        /// </summary>
        public List<FinalScoring> FSTList { set; get; }
        /// <summary>
        /// RoundBoostList
        /// </summary>
        public List<RoundBooster> RBTList { set; get; }
        /// <summary>
        /// AlianceList
        /// </summary>
        public List<AllianceTile> ALTList { set; get; }
        public MapActionMgr MapActionMrg { set; get; }
        public AllianceTile AllianceTileForTransForm { set; get; }
        [JsonProperty]
        public string UserActionLog { set; get; }
        [JsonProperty]
        public int Seed { set; get; }
        [JsonProperty]
        public string[] Username { set; get; }
        public Dictionary<string,List<Faction>> UserDic { set; get; }
        [JsonProperty]
        public bool IsTestGame { get; set; }
        [JsonProperty]
        public DateTime? LastMoveTime { set; get; }
        [JsonProperty]
        public int version { set; get; }
        public List<LogEntity> LogEntityList { set; get; }


        /// <summary>
        /// 分组
        /// </summary>
        public List<STTInfo> STT6ListGroup {
            set { }
            get
            {
                return STT6List.GroupBy(a => a.name).Select(g => new STTInfo { count = g.Count(), desc = g.Max(item => item.desc), name = g.Max(item => item.name),Index = g.Max(item=>item.Index)}).ToList();
            }
        }
        /// <summary>
        /// 分组
        /// </summary>
        public List<STTInfo> STT3ListGroup
        {
            set {}
            get
            {
                return STT3List.GroupBy(a => a.name).Select(g => new STTInfo { count = g.Count(), desc = g.Max(item => item.desc), name = g.Max(item => item.name), Index = g.Max(item => item.Index) }).ToList();
            }
        }

        public MapSelection MapSelection { get; private set; }
        public int UserCount { get; private set; }
        public string LastErrorLog { get; private set; }
        public Stack<string> RedoStack { set; get; }

        /// <summary>
        /// 用户列表
        /// </summary>
        [JsonProperty]
        public List<UserGameModel> UserGameModels { set; get; }
        /// <summary>
        /// 是否即时制
        /// </summary>
        [JsonProperty]
        public bool IsSocket { get; set; }
        /// <summary>
        /// 旋转地图
        /// </summary>
        [JsonProperty]
        public bool IsRotatoMap { get; set; }

        /// <summary>
        /// 禁止种族
        /// </summary>
        public List<Faction> JinzhiFaction { get; set; }


        /// <summary>
        /// 开始保存数据到数据库
        /// </summary>
        public bool IsSaveToDb { get; set; }

        /// <summary>
        /// drop 小时
        /// </summary>
        [JsonProperty]
        public int dropHour { get; set; }

        /// <summary>
        /// 聊天记录
        /// </summary>
        public List<GameChatModel> ListChatModels { get; set; }


        public class STTInfo
        {
            public STTInfo()
            {
                
            }
            public int count { get; set; }
            public string desc { get; set; }
            public string name { get; set; }
            public int? Index { get; set; }
        }


#endregion
    }
}
