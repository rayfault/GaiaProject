﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using GaiaCore.Util;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using GaiaCore.Gaia.Data;
using GaiaDbContext.Models;
using GaiaDbContext.Models.AccountViewModels;
using GaiaProject.Data;
using GaiaProject.Models.HomeViewModels;
using Microsoft.AspNetCore.Identity;

namespace GaiaCore.Gaia
{

    public static class GameMgr
    {
        private static Dictionary<string, GaiaGame> m_dic;
        static GameMgr()
        {
            m_dic = new Dictionary<string, GaiaGame>();
        }
        /// <summary>
        /// 创建游戏
        /// </summary>
        /// <param name="username"></param>
        /// <param name="model"></param>
        /// <param name="result"></param>
        /// <returns></returns>

        public static bool CreateNewGame(string[] username, NewGameViewModel model, out GaiaGame result,bool isSaveGame = false, UserManager<ApplicationUser> _userManager = null)
        {
            bool create = CreateNewGame(model.Name, username, out result, model.MapSelction, isTestGame: model.IsTestGame, isSocket: model.IsSocket, IsRotatoMap: model.IsRotatoMap, version: 4);
            if (_userManager != null)
            {
                //用户列表
                List<UserGameModel> listUser = new List<UserGameModel>();
                //判断用户不存在
                foreach (var item in username)
                {
                    var user = _userManager.FindByNameAsync(item);
                    if (user.Result == null)
                    {
                        //ModelState.AddModelError(string.Empty, item + "用户不存在");
                        //return View("NewGame");
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
                //赋值用户信息
                result.UserGameModels = listUser;
            }

            result.dropHour = model.dropHour;
            if (isSaveGame) { }
            return create;
        }
        /// <summary>
        /// 保存游戏到数据库
        /// </summary>
        /// <param name="model"></param>
        /// <param name="username"></param>
        /// <param name="jinzhiFaction"></param>
        /// <param name="dbContext"></param>
        /// <param name="result"></param>
        public static void SaveGameToDb(NewGameViewModel model,string username,string jinzhiFaction, ApplicationDbContext dbContext,GaiaGame result,int matchId = 0,string[] userlist = null)
        {
            //保存到数据库
            GaiaDbContext.Models.HomeViewModels.GameInfoModel gameInfoModel =
                new GaiaDbContext.Models.HomeViewModels.GameInfoModel()
                {
                    name = model.Name,

                    //UserCount = model.isHall?model.UserCount: username.Length,
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

                    //游戏大厅
                    isHall = model.isHall,
                    remark = model.remark,
                    dropHour = model.dropHour,

                    //比赛id
                    matchId = matchId,
                    //round = model.isHall?-1:0,
                };
            //游戏大厅
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
                //配置信息
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
                            .Select(item => string.Format("{0}{1}({2})", item.ChineseName,
                                "", item.UserName))); //最后的得分情况
            }
            dbContext.GameInfoModel.Add(gameInfoModel);
            dbContext.SaveChanges();
        }
        /// <summary>
        /// 创建游戏
        /// </summary>
        /// <param name="name"></param>
        /// <param name="username"></param>
        /// <param name="result"></param>
        /// <param name="MapSelection"></param>
        /// <param name="seed"></param>
        /// <param name="isTestGame"></param>
        /// <param name="isSocket"></param>
        /// <param name="IsRotatoMap"></param>
        /// <param name="version"></param>
        /// <returns></returns>

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
                result = new GaiaGame(username,name);
                result.IsTestGame = isTestGame;
                result.GameName = name;//游戏名称
                result.IsSocket = isSocket;//即时制
                result.IsRotatoMap = IsRotatoMap;//旋转地图
                result.version = version;

                //开局的两条命令
                result.Syntax(GameSyntax.setupmap + " " + MapSelection, out string log);
                result.Syntax(GameSyntax.setupGame + seed, out log);
                m_dic.Add(name, result);
                return true;
            }
        }

        public static void RemoveOldBackupData()
        {
            var d = new DirectoryInfo(BackupDataPath);
            //var filename = (from p in d.EnumerateFiles() orderby p.Name descending select p.Name).Take(5);
            //留下的文件数量
            int number = 0;
            //按名称排序
            List<FileInfo> listFile = (from p in d.EnumerateFiles() orderby p.Name descending select p).ToList();
            foreach (var item in listFile)
            {
                //超过5个备份就删除
                if (number > 50)
                {
                    item.Delete();
                }
                else
                {
                    //大于50K留下
                    if (item.Length > GameConfig.GAME_FILE_SIZE)
                    {
                        number++;
                    }
                    else
                    {
                        item.Delete();
                    }
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
            var logPath = System.IO.Path.Combine(FinishGamePath, name + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
            var logWriter = System.IO.File.CreateText(logPath);
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
                var result = from p in m_dic where p.Value.Username.Contains(userName) select p.Key;
                return result;
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

        public static bool BackupDictionary()
        {
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.ContractResolver = new LimitPropsContractResolver(new string[]
            {
                "GameName",  "UserActionLog", "Username", "IsTestGame", "LastMoveTime", "version",
                "UserGameModels","resetNumber","resetPayNumber","paygrade","username","remark","isTishi","IsSocket","IsRotatoMap","dropType","dropHour"
            });
            var str = JsonConvert.SerializeObject(m_dic, Formatting.Indented, jsetting);
            var logPath = System.IO.Path.Combine(BackupDataPath, DateTime.Now.ToString("yyyyMMddHHmmss") + "backup.txt");
            var logWriter = System.IO.File.CreateText(logPath);
            logWriter.Write(str);
            logWriter.Dispose();
            return true;
        }

        public static void WriteUserActionLog(string syntax,string username)
        {
            var str = string.Format("{0}/{1}/{2}\r\n", DateTime.Now.ToString(),username,syntax);
            var logPath = System.IO.Path.Combine(UserActionLogDataPath, DateTime.Now.ToString("yyyyMMddHH") + ".txt");
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

        public static async System.Threading.Tasks.Task<IEnumerable<string>> RestoreDictionaryFromServerAsync(string GameName = null, Func<string, bool> DebugInvoke = null)
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

            //设置用户信息
            gg.SetUserInfo();

            try
            {
                int rowIndex = 1;
                foreach (var str in item.Value.UserActionLog.Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (str.Contains("Xenos:build I3"))
                    {
                        int a = 1;
                    }
                    gg.Syntax(str, out string log);
                    if (!string.IsNullOrEmpty(log))
                    {
                        if (DebugInvoke != null)
                        {
                            DebugInvoke.Invoke(item.Key + ":" + log);
                        }
                        System.Diagnostics.Debug.WriteLine(item.Key + ":" + log);
                        break;
                    }
                    else
                    {
                        //System.Diagnostics.Debug.WriteLine(str);
                    }
                    if (row != null)
                    {
                        //相等，终止恢复
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
                System.Diagnostics.Debug.WriteLine(item.Key + ":" + ex.ToString());
            }
            //上次时间
            gg.LastMoveTime = item.Value.LastMoveTime;


            //需要加载到内存
            if (isTodict)
            {
                //如果是结束的游戏，跳过
                if (item.Value.GameStatus.stage == Stage.GAMEEND)
                {
                    
                }
                else//其它
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

        /// <summary>
        /// 返回读取到的文件
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
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
                return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "backupdata");
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
                return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "UserActionLogData");
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
                return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "finishgame");
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
