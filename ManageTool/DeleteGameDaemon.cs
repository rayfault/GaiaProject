﻿using GaiaCore.Gaia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GaiaCore.Gaia.Data;

namespace ManageTool
{
    /// <summary>
    /// 守护进程父类
    /// </summary>
    public class DeleteGameDaemon : Daemon
    {
        protected override int m_timeOut
        {
            get => 300 * 1000;
        }

        public override void InvokeAction()
        {
            var gamelist = GameMgr.GetAllGameName();
            foreach (var item in gamelist)
            {
                GaiaGame gaiaGame = GameMgr.GetGameByName(item);
                //删除结束游戏
                if (gaiaGame.GameStatus.stage == Stage.GAMEEND)
                {
                    GameMgr.RemoveAndBackupGame(item);
                    continue;
                }

                //判断上次行动时间是否大于设置drop时间

                int hours = gaiaGame.dropHour == 0 ? 240 : gaiaGame.dropHour;

                if (DateTime.Now.AddDays(-hours / 24) > gaiaGame.LastMoveTime)
                {
                    //GameMgr.RemoveAndBackupGame(item);
                    //不需要备份直接删除
                    //GameMgr.DeleteOneGame(item);

                    //记录用户drop 次数
                    //drop
                    String userName = gaiaGame.GetCurrentUserName();
                    //如果没有用户
                    //或者时2人游戏
                    //没有用户
                    //用户数量不等
                    //第0回合
                    //一场多个用户
                    if (String.IsNullOrEmpty(userName) || gaiaGame.UserCount == 2 || gaiaGame.UserGameModels == null ||
                        gaiaGame.UserGameModels.Count != gaiaGame.UserCount || gaiaGame.GameStatus.RoundCount == 0 ||
                        gaiaGame.UserGameModels.FindAll(user => user.username == userName).Count > 1)
                    {
                        GameMgr.DeleteOneGame(item);
                        continue;
                    }
                    else
                    {
                        try
                        {
                            Faction faction = gaiaGame.FactionList.SingleOrDefault((Faction fac) => fac.UserName == userName);
                            gaiaGame.Syntax(faction.FactionName.ToString() + ":drop", out string _, "", null);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
            }
        }
    }
}

