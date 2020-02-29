using GaiaCore.Gaia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GaiaCore.Gaia.Data;

namespace ManageTool
{
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
                //최종 게임 삭제
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
                    //백업하지 않고 삭제
                    //GameMgr.DeleteOneGame(item);

                    //记录用户drop 次数
                    //drop
                    String userName = gaiaGame.GetCurrentUserName();
                    //사용자가없는 경우
                    //아니면 2 인 게임
                    //사용자가 없습니다
                    //사용자 수
                    //라운드 0
                    //여러 사용자
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

