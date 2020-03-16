using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GaiaCore.Gaia.Data;
using GaiaCore.Util;
using GaiaDbContext.Models;
using GaiaDbContext.Models.AccountViewModels;
using GaiaDbContext.Models.HomeViewModels;
using GaiaProject.Data;
using GaiaProject.Models.HomeViewModels;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace GaiaCore.Gaia
{

    public static class GameMgr
    {
        private static Dictionary<string, GaiaGame> m_dic;
        static GameMgr()
        {
            m_dic = new Dictionary<string, GaiaGame>();
        }

        public static bool CreateNewGame(string[] username, NewGameViewModel model, out GaiaGame result,
            bool isSaveGame = false, UserManager<ApplicationUser> userManager = null)
        {
            var create = CreateNewGame(model.Name, username, out result, model.MapSelction,
                isTestGame: model.IsTestGame, isSocket: model.IsSocket, IsRotatoMap: model.IsRotatoMap, version: 4);

            if (userManager != null)
            {
                //사용자 목록
                var listUser = new List<UserGameModel>();

                //사용자가 존재하지 않는지 확인
                foreach (var item in username)
                {
                    var user = userManager.FindByNameAsync(item);
                    if (user.Result == null)
                    {
                        result = null;
                        return false;
                    }
                    else
                    {
                        listUser.Add(new UserGameModel()
                        {
                            username = item,
                            isTishi = true,
                            paygrade = user.Result.paygrade,
                            scoreUserStart = user.Result.scoreUser,
                        });
                    }
                }
                result.UserGameModels = listUser;
            }

            result.dropHour = model.dropHour;
            return create;
        }

        public static void SaveGameToDb(NewGameViewModel model,string username,string jinzhiFaction, ApplicationDbContext dbContext,GaiaGame result,int matchId = 0,string[] userlist = null)
        {
            var gameInfoModel = new GameInfoModel()
            {
                name = model.Name,

                MapSelction = model.MapSelction,
                IsTestGame = model.IsTestGame ? 1 : 0,
                GameStatus = 0,
                starttime = DateTime.Now,
                endtime = DateTime.Now,
                username = username,

                IsAllowLook = model.IsAllowLook,
                IsRandomOrder = model.IsRandomOrder,
                IsRotatoMap = model.IsRotatoMap,
                version = 4,

                //게임 로비
                isHall = model.isHall,
                remark = model.remark,
                dropHour = model.dropHour,

                //比赛id
                matchId = matchId,
                //round = model.isHall?-1:0,
            };

            if (model.isHall)
            {
                gameInfoModel.round = -1;
                gameInfoModel.UserCount = model.UserCount;
                gameInfoModel.userlist = string.Format("|{0}|", username);

            }
            else
            {
                gameInfoModel.round = 0;
                gameInfoModel.UserCount = userlist?.Length ?? 0;
                gameInfoModel.userlist = string.Join("|", userlist).Trim('|');

            }
            gameInfoModel.jinzhiFaction = jinzhiFaction;//this.HttpContext.Request.Form["jinzhi"];
            //有游戏信息
            if (result != null)
            {
                //구성 정보
                gameInfoModel.ATTList = string.Join("|", result.ATTList.Select(item => item.name));
                gameInfoModel.FSTList = string.Join("|", result.FSTList.Select(item => item.GetType().Name));
                gameInfoModel.RBTList = string.Join("|", result.RBTList.Select(item => item.name));
                gameInfoModel.RSTList = string.Join("|", result.RSTList.Select(item => item.GetType().Name));
                gameInfoModel.STT3List = string.Join("|",
                    result.STT3List.GroupBy(item => item.name).Select(g => g.Max(item => item.name)));
                gameInfoModel.STT6List = string.Join("|",
                    result.STT6List.GroupBy(item => item.name).Select(g => g.Max(item => item.name)));
                gameInfoModel.scoreFaction =
                    string.Join(":",
                        result.FactionList.OrderBy(item => item.Score)
                            .Select(item => string.Format("{0}{1}({2})", item.ChineseName, "", item.UserName))); //최종 채점 상황
            }
            dbContext.GameInfoModel.Add(gameInfoModel);
            dbContext.SaveChanges();
        }

        private static bool CreateNewGame(string name, string[] username, out GaiaGame result, string MapSelection, int seed = 0, bool isTestGame = false,bool isSocket = false,bool IsRotatoMap = false,int version=3)
        {
            if (m_dic.ContainsKey(name))
            {
                result = null;
                return false;
            }
            else
            {
                seed = seed == 0 ? RandomInstance.Next(int.MaxValue) : seed;
                result = new GaiaGame(username, name)
                {
                    IsTestGame = isTestGame,
                    GameName = name,
                    IsSocket = isSocket,
                    IsRotatoMap = IsRotatoMap,
                    version = version
                };

                //시작할 두 주문
                result.Syntax(GameSyntax.setupmap + " " + MapSelection, out string log);
                result.Syntax(GameSyntax.setupGame + seed, out log);
                m_dic.Add(name, result);
                return true;
            }
        }

        // 백업데이터를 이름으로 정렬하려 50개만 남기고 나머지는 지움
        public static void RemoveOldBackupData()
        {
            var d = new DirectoryInfo(BackupDataPath);
            var number = 0;
            var listFile = (from p in d.EnumerateFiles() orderby p.Name descending select p).ToList();
            foreach (var item in listFile)
            {
                if (number <= 50)
                {
                    number++;
                }
                else
                {
                    item.Delete();
                }
            }
        }

        public static GaiaGame GetGameByName(string name)
        {
            if (name == null)
            {
                return null;
            }
            if (m_dic.ContainsKey(name))
            {
                GaiaGame gaiaGame = m_dic[name];
                if (gaiaGame.GameName == null)
                {
                    gaiaGame.GameName = name;
                }
                return gaiaGame;
            }
            else
            {
                return null;
            }
        }

        public static bool RemoveAndBackupGame(string name)
        {
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.ContractResolver = new LimitPropsContractResolver(new string[] { "UserActionLog", "Username", "IsTestGame" });
            var str = JsonConvert.SerializeObject(m_dic[name], Formatting.Indented, jsetting);
            var logPath = Path.Combine(FinishGamePath, name + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
            var logWriter = File.CreateText(logPath);
            logWriter.Write(str);
            logWriter.Dispose();
            m_dic.Remove(name);
            return true;
        }

        public static IEnumerable<string> GetAllGameName(string userName = null)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return m_dic.Keys;
            }
            else
            {
                return from p in m_dic where p.Value.Username.Contains(userName) select p.Key;
            }
        }

        public static IEnumerable<KeyValuePair<string, GaiaGame>> GetAllGame(string userName = null)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return m_dic;
            }
            else
            {
                var result = from p in m_dic where p.Value.Username.Contains(userName) select p;
                return result;
            }
        }

        public static string GetNextGame(string userName = null)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return string.Empty;
            }
            else
            {
                var result = GetAllGameName(userName).ToList().Find(x =>
                {
                    var gg = GetGameByName(x);
                    if (gg.UserGameModels==null || gg.UserGameModels.All(item => item.username != userName) || !(gg.UserGameModels.Find(item => item.username == userName).isTishi))
                    {
                        return false;
                    }
                    else
                    {
                        var isLeech = gg.UserDic.Count > 1 && gg.GameStatus.stage == Stage.ROUNDWAITLEECHPOWER && gg.UserDic.ContainsKey(userName) && gg.UserDic[userName].Exists(y => y.LeechPowerQueue.Count != 0);
                        bool flag = isLeech || (gg.UserDic.Count > 1 && gg.GetCurrentUserName().Equals(userName) && gg.GameStatus.stage != Stage.GAMEEND);
                        return flag;
                    }
                });
                return result;
            }
        }

        public static void BackupDictionary()
        {
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.ContractResolver = new LimitPropsContractResolver(new string[]
            {
                "GameName",  "UserActionLog", "Username", "IsTestGame", "LastMoveTime", "version",
                "UserGameModels","resetNumber","resetPayNumber","paygrade","username","remark","isTishi","IsSocket","IsRotatoMap","dropType","dropHour"
            });
            var str = JsonConvert.SerializeObject(m_dic, Formatting.Indented, jsetting);
            if ("{}".Equals(str))
            {
                // 백업데이터가 없을 경우, 패스
                return;
            }
            var logPath = Path.Combine(BackupDataPath, DateTime.Now.ToString("yyyyMMddHHmmss") + "backup.txt");
            Console.WriteLine("========== Log : " + logPath + " ==========");
            File.WriteAllText(logPath, str + "\r\n");
        }

        public static void WriteUserActionLog(string syntax,string username)
        {
            var str = string.Format("{0}/{1}/{2}\r\n", DateTime.Now.ToString(),username,syntax);
            var logPath = Path.Combine(UserActionLogDataPath, DateTime.Now.ToString("yyyyMMddHH") + ".txt");
            File.AppendAllText(logPath,str);
        }

        public static IEnumerable<string> RestoreDictionary(string filename)
        {
            string logReader = GetLastestBackupData(filename);
            if (string.IsNullOrEmpty(logReader))
            {
                return null;
            }
            return RestoreAllGames(logReader);
        }

        public static async Task<IEnumerable<string>> RestoreDictionaryFromServerAsync(string GameName = null, Func<string, bool> DebugInvoke = null)
        {
            HttpClient client = new HttpClient();
            var logReader = await client.GetStringAsync("http://gaiaproject.chinacloudsites.cn/home/GetLastestActionLog");
            return RestoreAllGames(logReader, GameName, "yucenyucen@126.com", DebugInvoke);
            //return RestoreAllGames(logReader, GameName, DebugInvoke: DebugInvoke);
        }

        private static IEnumerable<string> RestoreAllGames(string logReader, string GameName = null, string user = null, Func<string, bool> DebugInvoke = null)
        {
            var temp = JsonConvert.DeserializeObject<Dictionary<string, GaiaGame>>(logReader);
            m_dic = new Dictionary<string, GaiaGame>();
            foreach (var item in temp)
            {
                if (!string.IsNullOrEmpty(GameName) && !item.Key.Equals(GameName))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(user))
                {
                    for (int i = 0; i < item.Value.Username.Where(x => !string.IsNullOrEmpty(x)).Count(); i++)
                    {
                        item.Value.Username[i] = user;
                    }
                }
                RestoreGameWithActionLog(item, DebugInvoke);

            }
            return m_dic.Keys;
        }

        private static GaiaGame RestoreGameWithActionLog(KeyValuePair<string, GaiaGame> item, Func<string, bool> DebugInvoke = null,bool isTodict=true,int? row=null)
        {
            var gg = new GaiaGame(item.Value.Username,item.Value.GameName);
            gg.IsTestGame = item.Value.IsTestGame;//测试
            gg.IsSocket = item.Value.IsSocket;//即使制度
            gg.IsRotatoMap = item.Value.IsRotatoMap;//旋转地图
            gg.IsSaveToDb = item.Value.IsSaveToDb;//是否保存数据
            gg.dbContext = item.Value.dbContext;//数据源

            gg.UserGameModels = item.Value.UserGameModels;//恢复设置

            if (item.Value.version == 0)
            {
                gg.version = 1;
            }
            else
            {
                gg.version = item.Value.version;
            }

            //사용자 정보 설정
            gg.SetUserInfo();

            try
            {
                int rowIndex = 1;
                foreach (var str in item.Value.UserActionLog.Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    gg.Syntax(str, out string log);
                    if (!string.IsNullOrEmpty(log))
                    {
                        if (DebugInvoke != null)
                        {
                            DebugInvoke.Invoke(item.Key + ":" + log);
                        }
                        Debug.WriteLine(item.Key + ":" + log);
                        break;
                    }
                    else
                    {
                        //System.Diagnostics.Debug.WriteLine(str);
                    }
                    if (row != null)
                    {
                        //동일, 복구 종료
                        if (rowIndex == row)
                        {
                            break;
                        }
                        else
                        {
                            rowIndex++;
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                if (DebugInvoke != null)
                {
                    DebugInvoke.Invoke(item.Key + ":" + ex.ToString());
                }
                Debug.WriteLine(item.Key + ":" + ex.ToString());
            }
            //마지막으로
            gg.LastMoveTime = item.Value.LastMoveTime;


            //메모리에로드해야합니다
            if (isTodict)
            {
                //게임이 끝나면 건너 뛰십시오
                if (item.Value.GameStatus.stage == Stage.GAMEEND)
                {
                    
                }
                else
                {
                    if (m_dic.ContainsKey(item.Key))
                    {
                        m_dic[item.Key] = gg;
                    }
                    else
                    {
                        m_dic.Add(item.Key, gg);
                    }
                }

            }
            else
            {
//                if (m_dic.ContainsKey(item.Key))
//                {
//                    m_dic.Remove(item.Key);
//                }
            }
            return gg;
        }

        public static bool ReportBug(string id)
        {
            var gg = GetGameByName(id);
            if (gg == null)
            {
                return false;
            }
            string[] newUserName = new string[4];
            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(gg.Username[i]))
                {
                    newUserName[i] = null;
                }
                else
                {
                    newUserName[i] = "Report@Bug.com";
                }
            }
            var ggNew = new GaiaGame(newUserName);
            ggNew.UserActionLog = gg.UserActionLog;
            RestoreGameWithActionLog(new KeyValuePair<string, GaiaGame>(id + "Debug" + DateTime.Now.ToString("yyyyMMddHHmmss"), ggNew));
            return true;
        }

        public static void ChangeAllGamesUsername(string userName1, string userName2)
        {
            if (string.IsNullOrEmpty(userName1) || string.IsNullOrEmpty(userName2))
            {
                return;
            }
            foreach (var item in m_dic)
            {
                for (int i = 0; i < item.Value.Username.Length; i++)
                {
                    if (item.Value.Username[i] != null && item.Value.Username[i].Equals(userName1))
                    {
                        item.Value.Username[i] = userName2;
                    }
                }
                var needModify = item.Value.UserDic.Where(x => x.Key.Equals(userName1));
                needModify.ToList().ForEach(x =>
                {
                    item.Value.UserDic.Add(userName2, x.Value);
                    item.Value.UserDic.Remove(x.Key);
                });
                foreach(var fac in item.Value.FactionList)
                {
                    if (fac.UserName.Equals(userName1))
                    {
                        fac.UserName = userName2;
                    }
                }
            }
        }

        public static bool RedoOneStep(string id)
        {
            var gg = GetGameByName(id);
            if (gg == null|| !gg.RedoStack.Any())
            {
                return false;
            }
            var syntax = gg.RedoStack.Pop();
            gg.Syntax(syntax, out string log);
            return true;
        }

        public static bool DeleteOneGame(string id)
        {
            if (m_dic.ContainsKey(id))
            {
                m_dic.Remove(id);
            }
            return true;
        }

        public static void DeleteAllGame()
        {
            m_dic.Clear();
        }

        public static string GetLastestBackupData(string filename = null)
        {
            if (string.IsNullOrEmpty(filename))
            {
                var d = new DirectoryInfo(BackupDataPath);
                filename = (from p in d.EnumerateFiles() where p.Length > GameConfig.GAME_FILE_SIZE orderby p.Name descending select p.Name).FirstOrDefault();
            }
            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }
            var logPath = Path.Combine(BackupDataPath, filename);
            var logReader = File.ReadAllText(logPath);
            return logReader;
        }

        public static IEnumerable<string> GetAllBackupDataName()
        {
            var d = new DirectoryInfo(BackupDataPath);
            return from p in d.EnumerateFiles() select p.Name;
        }

        private static string BackupDataPath
        {
            get
            {
                if (!Directory.Exists("backupdata"))
                {
                    Directory.CreateDirectory("backupdata");
                }
                return Path.Combine(Directory.GetCurrentDirectory(), "backupdata");
            }
        }

        private static string UserActionLogDataPath
        {
            get
            {
                if (!Directory.Exists("UserActionLogData"))
                {
                    Directory.CreateDirectory("UserActionLogData");
                }
                return Path.Combine(Directory.GetCurrentDirectory(), "UserActionLogData");
            }
        }

        private static string FinishGamePath
        {
            get
            {
                if (!Directory.Exists("finishgame"))
                {
                    Directory.CreateDirectory("finishgame");
                }
                return Path.Combine(Directory.GetCurrentDirectory(), "finishgame");
            }
        }

        public static bool UndoOneStep(string GameName)
        {
            var gg = GetGameByName(GameName);
            if (gg == null)
            {
                return false;
            }
            var syntaxList = gg.UserActionLog.Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (syntaxList.Last().StartsWith("#"))
            {
                while (syntaxList.Last().StartsWith("#"))
                {
                    syntaxList.RemoveAt(syntaxList.Count - 1);
                }
            }
            gg.RedoStack.Push(syntaxList.Last());
            var Redo = gg.RedoStack;
            syntaxList.RemoveAt(syntaxList.Count - 1);


            gg.UserActionLog = string.Join("\r\n", syntaxList);

            RestoreGameWithActionLog(new KeyValuePair<string, GaiaGame>(GameName, gg));
            GetGameByName(GameName).RedoStack = Redo;
            return true;
        }

        public static GaiaGame RestoreGame(string GameName,GaiaGame gg,bool isToDict = false,int? row=null)
        {
            return RestoreGameWithActionLog(new KeyValuePair<string, GaiaGame>(GameName, gg),null, isToDict, row:row);
        }
    }
}
