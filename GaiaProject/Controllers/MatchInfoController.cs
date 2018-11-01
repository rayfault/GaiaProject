using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GaiaCore.Gaia;
using GaiaCore.Gaia.Data;
using GaiaCore.Gaia.Game;
using GaiaDbContext.Models;
using GaiaDbContext.Models.HomeViewModels;
using GaiaProject.Data;
using GaiaProject.Models.HomeViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GaiaProject.Controllers
{
    public class MatchInfoController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public MatchInfoController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            this.dbContext = dbContext;
        }
        public IActionResult Index()
        {
            var list = this.dbContext.MatchInfoModel.ToList();
            return View(list);
        }
        /// <summary>
        /// 添加比赛
        /// </summary>
        /// <returns></returns>
        [HttpGet]

        public IActionResult AddMatch()
        {
            return View();
        }
        /// <summary>
        /// 添加比赛
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]

        public IActionResult AddMatch(GaiaDbContext.Models.HomeViewModels.MatchInfoModel model)
        {
            if (ModelState.IsValid)
            {
                this.dbContext.MatchInfoModel.Add(model);
                this.dbContext.SaveChanges();
                return Redirect("Index");
            }
            return View(model);
        }
        /// <summary>
        /// 显示比赛详细
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public IActionResult MatchShow(int id)
        {
            MatchInfoModel matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                //查询当前报名人
                IQueryable<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).OrderByDescending(item=>item.Score);

                //查询当前比赛
                IQueryable<GameInfoModel> gameInfoModels = this.dbContext.GameInfoModel.Where(item => item.matchId == matchInfoModel.Id);


                MatchShowModel matchShowModel = new MatchShowModel()
                {
                    MatchInfoModel = matchInfoModel,
                    MatchJoinModels = matchJoinModels,
                    GameInfoModels = gameInfoModels,
                };
                return View(matchShowModel);
            }
            return View(null);
        }

        /// <summary>
        /// 删除比赛
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> DelMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            MatchInfoModel matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                this.dbContext.MatchInfoModel.Remove(matchInfoModel);
                this.dbContext.SaveChanges();
                jsonData.info.state = 200;
            }
            return new JsonResult(jsonData);

        }


        /// <summary>
        /// 详细
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> ShowInfo(int id)
        {
            //主要信息
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            //jsonData.data = matchInfoModel;
            //查询当前报名人
            List<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).ToList();

            //查询当前比赛
            //List<GameInfoModel> gameInfoModels = this.dbContext.GameInfoModel.Where(item => item.matchId == matchInfoModel.Id).ToList();

            jsonData.data = new
            {
                matchInfoModel = matchInfoModel,
                matchJoinModels = matchJoinModels,
                //gameInfoModels = gameInfoModels,
            };
            jsonData.info.state = 200;
            return new JsonResult(jsonData);
        }
        /// <summary>
        /// 加入比赛
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> JoinMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            var user = await _userManager.GetUserAsync(HttpContext.User);

            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                if (this.dbContext.MatchJoinModel.Any(
                    item => item.matchInfo_id == matchInfoModel.Id && item.username == user.UserName))
                {
                    jsonData.info.state = 0;
                    jsonData.info.message = "已经报名";

                }
                else
                {
                    //是否已经满足报名
                    if (matchInfoModel.NumberMax !=0 &&matchInfoModel.NumberNow == matchInfoModel.NumberMax)
                    {
                        jsonData.info.state = 0;
                        jsonData.info.message = "报名人员已满";
                    }
                    else if (matchInfoModel.RegistrationEndTime<DateTime.Now)
                    {
                        jsonData.info.state = 0;
                        jsonData.info.message = "报名时间截止";
                    }
                    else
                    {
                        //报名人数+1
                        matchInfoModel.NumberNow++;
                        this.dbContext.MatchInfoModel.Update(matchInfoModel);
                        //报名信息
                        MatchJoinModel matchJoinModel = new MatchJoinModel();
                        matchJoinModel.matchInfo_id = id;//id
                        matchJoinModel.Name = matchInfoModel.Name;//name

                        matchJoinModel.AddTime = DateTime.Now;//时间
                        matchJoinModel.username = user.UserName;
                        matchJoinModel.userid = user.Id;

                        matchJoinModel.Rank = 0;
                        matchJoinModel.Score = 0;

                        this.dbContext.MatchJoinModel.Add(matchJoinModel);
                        this.dbContext.SaveChanges();

                        jsonData.info.state = 200;
                     
                        this.AutoCreateMatch(matchInfoModel);
                    }
                }
            }
            return new JsonResult(jsonData);
        }
        /// <summary>
        /// 自动创建比赛
        /// </summary>
        private void AutoCreateMatch(MatchInfoModel matchInfoModel)
        {
            //是否自动创建比赛
            //并且只能创建7人的比赛
            if (matchInfoModel.IsAutoCreate && matchInfoModel.NumberNow == matchInfoModel.NumberMax && matchInfoModel.NumberNow == 7)
            {
                //取出全部的报名人员
                List<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).ToList();

                if (matchJoinModels.Count() == 7)
                {
                    //int[7][4] rank = new int();

                    int[,] userOrder = new int[7, 4] { { 1,5,4,3 }, { 5,2,6,1 },{2,4,0,5},{4,6,3,2},{6,0,1,4},{0,3,5,6},{3,1,2,0}};

                    //创建游戏
                    NewGameViewModel newGameViewModel = new NewGameViewModel()
                    {
                        dropHour = 72,
                        IsAllowLook = true,
                        isHall = false,
                        IsRandomOrder = false,
                        IsRotatoMap = true,
                        IsSocket = false,
                        IsTestGame = false,
                        jinzhiFaction = null,
                        MapSelction = GameInfoAttribute.MapSelction[GameInfoAttribute.MapSelction.Count - 1].code,
                        //Name = matchInfoModel.GameName,
                    };

                    for (int i = 0;i<7;i++ )
                    {
                        string[] users= new string[]
                        {
                            matchJoinModels[userOrder[i, 0]].username, matchJoinModels[userOrder[i, 1]].username,matchJoinModels[userOrder[i, 2]].username,matchJoinModels[userOrder[i, 3]].username
                        };
                        //游戏名称
                        if (matchInfoModel.GameName.Contains("{0}"))
                        {
                            newGameViewModel.Name = string.Format(matchInfoModel.GameName, i + 1);
                        }
                        else
                        {
                            newGameViewModel.Name = string.Concat(matchInfoModel.GameName, i + 1);
                        }
                        //创建游戏到内存
                        GameMgr.CreateNewGame(users, newGameViewModel, out GaiaGame gaiaGame);
                        //保存到数据库
                        GameMgr.SaveGameToDb(newGameViewModel, "gaia", null, this.dbContext, gaiaGame);

                    }



                }
                //将游戏标准为进行状态
                matchInfoModel.State = 1;
                this.dbContext.MatchInfoModel.Update(matchInfoModel);
                this.dbContext.SaveChanges();           

            }
        }

        /// <summary>
        /// 退出比赛
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> ExitMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            //比赛信息
            MatchInfoModel matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                if (matchInfoModel.RegistrationEndTime < DateTime.Now)
                {
                    jsonData.info.state = 0;
                    jsonData.info.message = "报名时间截止";
                }
                else if (matchInfoModel.State!=0)
                {
                    jsonData.info.state = 0;
                    jsonData.info.message = "已经开始，无法退出";
                }
                else
                {
                    MatchJoinModel matchJoinModel =
                        this.dbContext.MatchJoinModel.SingleOrDefault(item => item.matchInfo_id == matchInfoModel.Id &&
                                                                              item.username == HttpContext.User.Identity
                                                                                  .Name);
                    if (matchJoinModel != null)
                    {
                        //删除
                        this.dbContext.MatchJoinModel.Remove(matchJoinModel);
                        //报名人数-1
                        matchInfoModel.NumberNow--;
                        this.dbContext.MatchInfoModel.Update(matchInfoModel);

                        this.dbContext.SaveChanges();

                        jsonData.info.state = 200;
                    }
                    else
                    {
                        jsonData.info.state = 0;
                        jsonData.info.message = "没有报名";

                    }
                }
            }

            return new JsonResult(jsonData);
        }
        /// <summary>
        /// 将游戏添加到比赛
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddGameToMatch(int id,string gameid)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            //比赛信息
            GameInfoModel gameInfoModel = this.dbContext.GameInfoModel.SingleOrDefault(item => item.Id == int.Parse(gameid));
            if (gameInfoModel != null)
            {
                gameInfoModel.matchId = id;
                this.dbContext.GameInfoModel.Update(gameInfoModel);
            
                this.dbContext.SaveChanges();
                jsonData.info.state = 200;
            }
            else
            {
                jsonData.info.state = 0;
                jsonData.info.message = "没有报名";
            }
            return new JsonResult(jsonData);
        }

        /// <summary>
        /// 将用户添加到比赛
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddUserToMatch(int id, string username)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                ApplicationUser applicationUser = this.dbContext.Users.SingleOrDefault(item => item.UserName == username);
                if (applicationUser != null)
                {
                    MatchJoinModel matchJoinModel = new MatchJoinModel();
                    matchJoinModel.matchInfo_id = id; //id
                    matchJoinModel.Name = matchInfoModel.Name; //name

                    matchJoinModel.AddTime = DateTime.Now; //时间
                    matchJoinModel.username = applicationUser.UserName;
                    matchJoinModel.userid = applicationUser.Id;

                    matchJoinModel.Rank = 0;
                    matchJoinModel.Score = 0;


                    //用户数量
                    matchInfoModel.NumberNow++;
                    this.dbContext.MatchInfoModel.Update(matchInfoModel);

                    this.dbContext.MatchJoinModel.Add(matchJoinModel);
                    this.dbContext.SaveChanges();

                    this.AutoCreateMatch(matchInfoModel);
                    jsonData.info.state = 200;
                    return new JsonResult(jsonData);
                }
                else
                {
                    jsonData.info.message = "用户不存在";
                }
                
            }
            else
            {
                jsonData.info.message = "比赛不存在";
            }
            jsonData.info.state = 0;
            return new JsonResult(jsonData);
        }
        /// <summary>
        /// 从游戏将用户添加
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddUserFromGame(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            IQueryable<GameInfoModel> gameInfoModels = this.dbContext.GameInfoModel.Where(item => item.matchId == id);
            List<string> userList = new List<string>();
            foreach (GameInfoModel gameInfoModel in gameInfoModels)
            {
                string[] users = gameInfoModel.userlist.Split("|");
                foreach (string user in users)
                {
                    if (!userList.Contains(user))
                    {
                        userList.Add(user);
                    }
                }
            }
            if (userList.Count == 7)
            {
                userList.ForEach((user) =>
                {
                    AddUserToMatch(id, user);
                });
            }
            jsonData.info.state = 200;
            return new JsonResult(jsonData);

        }

        /// <summary>
        /// 计分
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> ScoreMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            //主要信息
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            //jsonData.data = matchInfoModel;
            


            //分数清零
            //查询当前报名人
            List<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel
                .Where(item => item.matchInfo_id == matchInfoModel.Id).ToList();
            foreach (MatchJoinModel matchJoinModel in matchJoinModels)
            {
                matchJoinModel.Score = 0;
                matchJoinModel.first = 0;
                matchJoinModel.second = 0;
                matchJoinModel.third = 0;
                matchJoinModel.fourth = 0;
                this.dbContext.MatchJoinModel.Update(matchJoinModel);
            }


            //查询当前比赛
            IQueryable<GameInfoModel> gameInfoModels = this.dbContext.GameInfoModel.Where(item => item.matchId == matchInfoModel.Id);
            //遍历比赛
            foreach (GameInfoModel gameInfoModel in gameInfoModels)
            {
                DbGameSave.SaveMatchToDb(gameInfoModel,dbContext);
            }
            this.dbContext.SaveChanges();
            jsonData.info.state = 200;

            return new JsonResult(jsonData);
            return null;
        }

        [HttpPost]
        public async Task<JsonResult> SetJoin(int id,Int16 state)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            matchInfoModel.State = (short) (matchInfoModel.State==0?1:0);
            this.dbContext.MatchInfoModel.Update(matchInfoModel);
            this.dbContext.SaveChanges();
            jsonData.info.state = 200;
            return new JsonResult(jsonData);

        }
    }
}