using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaia.Model.Match;
using GaiaCore.Gaia;
using GaiaCore.Gaia.Data;
using GaiaCore.Gaia.Game;
using GaiaDbContext.Models;
using GaiaDbContext.Models.HomeViewModels;
using GaiaProject.Data;
using GaiaProject.Models.HomeViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
            var list = this.dbContext.MatchInfoModel.ToList().OrderByDescending(item=>item.Id).ToList();
            return View(list);
        }

        #region ���������͹���

        /// <summary>
        /// ��ӱ���
        /// </summary>
        /// <returns></returns>
        [HttpGet]

        public IActionResult AddMatch(int? id)
        {
            if (id != null)
            {
                MatchInfoModel matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
                return View(matchInfoModel);
            }
            return View();
        }
        /// <summary>
        /// ��ӱ���
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]

        public IActionResult AddMatch(GaiaDbContext.Models.HomeViewModels.MatchInfoModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Id > 0)
                {
                    MatchInfoModel matchInfoModel =
                        this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == model.Id);
                    matchInfoModel.Name = model.Name;
                    matchInfoModel.Contents = model.Contents;
                    matchInfoModel.IsAutoCreate = model.IsAutoCreate;
                    matchInfoModel.NumberMax = model.NumberMax;
                    //matchInfoModel.Name = model.Name;
                    this.dbContext.MatchInfoModel.Update(matchInfoModel);
                }
                else
                {
                    this.dbContext.MatchInfoModel.Add(model);
                }
                this.dbContext.SaveChanges();
                return Redirect("/MatchInfo/Index");
            }
            return View(model);
        }

        public IActionResult MatchShow(int id)
        {
            MatchInfoModel matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                IQueryable<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).OrderByDescending(item => item.Score);

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
        /// ɾ������
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
        /// ��ϸ
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> ShowInfo(int id)
        {
            //��Ҫ��Ϣ
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            //jsonData.data = matchInfoModel;
            //��ѯ��ǰ������
            List<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).ToList();

            //��ѯ��ǰ����
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
        /// �������
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> JoinMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            var user = await _userManager.GetUserAsync(HttpContext.User);

            if (user.isallowmatch != 1)
            {
                jsonData.info.state = 0;
                jsonData.info.message = "�㱻��ֹ�μ�Ⱥ����������ϵ����Ա��ѯԭ��!";
            }
            else
            {
                var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
                //������Ϣ��Ϊ��
                if (matchInfoModel != null)
                {
                    if (this.dbContext.MatchJoinModel.Any(
                        item => item.matchInfo_id == matchInfoModel.Id && item.username == user.UserName))
                    {
                        jsonData.info.state = 0;
                        jsonData.info.message = "�Ѿ�����";

                    }
                    else
                    {
                        //�Ƿ��Ѿ����㱨��
                        if (matchInfoModel.NumberMax != 0 && matchInfoModel.NumberNow == matchInfoModel.NumberMax)
                        {
                            jsonData.info.state = 0;
                            jsonData.info.message = "������Ա����";
                        }
                        else if (matchInfoModel.RegistrationEndTime < DateTime.Now)
                        {
                            jsonData.info.state = 0;
                            jsonData.info.message = "����ʱ���ֹ";
                        }
                        else
                        {
                            //��������+1
                            matchInfoModel.NumberNow++;
                            this.dbContext.MatchInfoModel.Update(matchInfoModel);
                            //������Ϣ
                            MatchJoinModel matchJoinModel = new MatchJoinModel();
                            matchJoinModel.matchInfo_id = id;//id
                            matchJoinModel.Name = matchInfoModel.Name;//name

                            matchJoinModel.AddTime = DateTime.Now;//ʱ��
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
            }
            
            return new JsonResult(jsonData);
        }
        /// <summary>
        /// �Զ���������
        /// </summary>
        private void AutoCreateMatch(MatchInfoModel matchInfoModel)
        {
            //�Ƿ��Զ���������
            //����ֻ�ܴ���7�˵ı���
            if (matchInfoModel.IsAutoCreate && matchInfoModel.NumberNow == matchInfoModel.NumberMax && matchInfoModel.NumberNow == 7)
            {
                //ȡ��ȫ���ı�����Ա
                List<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).ToList();

                if (matchJoinModels.Count() == 7)
                {
                    //int[7][4] rank = new int();

                    int[,] userOrder = new int[7, 4] { { 1, 5, 4, 3 }, { 5, 2, 6, 1 }, { 2, 4, 0, 5 }, { 4, 6, 3, 2 }, { 6, 0, 1, 4 }, { 0, 3, 5, 6 }, { 3, 1, 2, 0 } };

                    //������Ϸ
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

                    for (int i = 0; i < 7; i++)
                    {
                        string[] users = new string[]
                        {
                            matchJoinModels[userOrder[i, 0]].username, matchJoinModels[userOrder[i, 1]].username,matchJoinModels[userOrder[i, 2]].username,matchJoinModels[userOrder[i, 3]].username
                        };
                        //��Ϸ����
                        if (matchInfoModel.GameName.Contains("{0}"))
                        {
                            newGameViewModel.Name = string.Format(matchInfoModel.GameName, i + 1);
                        }
                        else
                        {
                            newGameViewModel.Name = string.Concat(matchInfoModel.GameName, i + 1);
                        }
                        //������Ϸ���ڴ�
                        GameMgr.CreateNewGame(users, newGameViewModel, out GaiaGame gaiaGame, userManager: _userManager);
                        //���浽���ݿ�
                        GameMgr.SaveGameToDb(newGameViewModel, "gaia", null, this.dbContext, gaiaGame, matchId: matchInfoModel.Id, userlist: users);

                    }



                }
                //����Ϸ��׼Ϊ����״̬
                matchInfoModel.State = 1;
                //��������
                matchInfoModel.MatchTotalNumber = 7;
                //��ʼʱ��
                matchInfoModel.StartTime = DateTime.Now;

                this.dbContext.MatchInfoModel.Update(matchInfoModel);
                this.dbContext.SaveChanges();

            }
        }

        /// <summary>
        /// �˳�����
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> ExitMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            //������Ϣ
            MatchInfoModel matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel != null)
            {
                if (matchInfoModel.RegistrationEndTime < DateTime.Now)
                {
                    jsonData.info.state = 0;
                    jsonData.info.message = "����ʱ���ֹ";
                }
                else if (matchInfoModel.State != 0)
                {
                    jsonData.info.state = 0;
                    jsonData.info.message = "�Ѿ���ʼ���޷��˳�";
                }
                else
                {
                    MatchJoinModel matchJoinModel =
                        this.dbContext.MatchJoinModel.SingleOrDefault(item => item.matchInfo_id == matchInfoModel.Id &&
                                                                              item.username == HttpContext.User.Identity
                                                                                  .Name);
                    if (matchJoinModel != null)
                    {
                        //ɾ��
                        this.dbContext.MatchJoinModel.Remove(matchJoinModel);
                        //��������-1
                        matchInfoModel.NumberNow--;
                        this.dbContext.MatchInfoModel.Update(matchInfoModel);

                        this.dbContext.SaveChanges();

                        jsonData.info.state = 200;
                    }
                    else
                    {
                        jsonData.info.state = 0;
                        jsonData.info.message = "û�б���";

                    }
                }
            }

            return new JsonResult(jsonData);
        }

        #endregion


        #region �û�����Ϸ��������Լ��Ʒ�
        /// <summary>
        /// ����Ϸ��ӵ�����
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddGameToMatch(int id, string gameid)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            //������Ϣ
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
                jsonData.info.message = "û�б���";
            }
            return new JsonResult(jsonData);
        }

        /// <summary>
        /// ���û���ӵ�����
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

                    matchJoinModel.AddTime = DateTime.Now; //ʱ��
                    matchJoinModel.username = applicationUser.UserName;
                    matchJoinModel.userid = applicationUser.Id;

                    matchJoinModel.Rank = 0;
                    matchJoinModel.Score = 0;


                    //�û�����
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
                    jsonData.info.message = "�û�������";
                }

            }
            else
            {
                jsonData.info.message = "����������";
            }
            jsonData.info.state = 0;
            return new JsonResult(jsonData);
        }
        /// <summary>
        /// ����Ϸ���û����
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
        /// �Ʒ�
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> ScoreMatch(int id)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            //��Ҫ��Ϣ
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            if (matchInfoModel == null)
            {

            }
            else
            {
                this.ScoreMatch(matchInfoModel);
                this.dbContext.SaveChanges();
                jsonData.info.state = 200;
            }
            return new JsonResult(jsonData);
        }
        /// <summary>
        /// ȫ��û�����ļƷ�
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult ScoreAll()
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            //�����ı��2
            IQueryable<MatchInfoModel> matchInfoModels = this.dbContext.MatchInfoModel.Where(item => item.State != 2 && item.State==1);
            foreach (MatchInfoModel matchInfoModel in matchInfoModels)
            {
                this.ScoreMatch(matchInfoModel);
            }
            this.dbContext.SaveChanges();
            return View("Index", this.dbContext.MatchInfoModel.ToList().OrderByDescending(item => item.Id).ToList());
        }
        /// <summary>
        /// �Ʒ�
        /// </summary>
        /// <param name="matchInfoModel"></param>
        private void ScoreMatch(MatchInfoModel matchInfoModel)
        {
            //jsonData.data = matchInfoModel;
            matchInfoModel.MatchFinishNumber = 0;
            //��������
            //��ѯ��ǰ������
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
            //��ѯ��ǰ����
            IQueryable<GameInfoModel> gameInfoModels = this.dbContext.GameInfoModel.Where(item => item.matchId == matchInfoModel.Id);

            //��������
            foreach (GameInfoModel gameInfoModel in gameInfoModels)
            {
                bool isFinish = DbGameSave.SaveMatchToDb(gameInfoModel, dbContext);
                if (isFinish)
                {
                    matchInfoModel.MatchFinishNumber++;
                }
            }

            //���7��ȫ�����������¹ھ�
            int gameCount = gameInfoModels.Count();
            //����������������г���
            if (matchInfoModel.MatchFinishNumber == gameCount)
            {
                //��ѯ������ߵ�
                IQueryable<MatchJoinModel> match = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == matchInfoModel.Id).OrderByDescending(item => item.Score);
                String username = match.ToList()[0].username;
                matchInfoModel.Champion = username;
                //������1�����2
                matchInfoModel.State = 2;

                //matchInfoModel.MatchFinishNumber = (short) gameCount;
                //����ʱ��
                matchInfoModel.EndTime = DateTime.Now;
                //matchInfoModel.MatchTotalNumber = (short)gameCount;
            }
            this.dbContext.MatchInfoModel.Update(matchInfoModel);
        }

        /// <summary>
        /// �޸�״̬
        /// </summary>
        /// <param name="id"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> SetJoin(int id, Int16 state)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();
            var matchInfoModel = this.dbContext.MatchInfoModel.SingleOrDefault(item => item.Id == id);
            matchInfoModel.State = (short)(matchInfoModel.State == 0 ? 1 : 0);
            this.dbContext.MatchInfoModel.Update(matchInfoModel);
            this.dbContext.SaveChanges();
            jsonData.info.state = 200;
            return new JsonResult(jsonData);

        }
        /// <summary>
        /// �鿴�û���ȫ������
        /// </summary>
        /// <returns></returns>
        public IActionResult UserScoreTotal()
        {
            var list = this.dbContext.MatchJoinModel.GroupBy(item => item.username).Select(g => new MatchUserStatistics
            {
                UserName = g.Max(user => user.username),
                Count = g.Count(),
                ScoreTotal = g.Sum(user => user.Score),
                ScoreAvg = g.Sum(user => user.Score) / g.Count(),

            }).OrderByDescending(item => item.ScoreAvg).ToList();
            //            if (list.Count > 20)
            //            {
            //                list = list.[20];
            //            }
            return View(list);
        }


        #endregion



        #region �ֶ��޸��û�����

        public IActionResult MatchJoinList(int id)
        {
            IQueryable<MatchJoinModel> matchJoinModels = this.dbContext.MatchJoinModel.Where(item => item.matchInfo_id == id);
            return View(matchJoinModels);
        }

        public IActionResult MatchJoinModify(int id)
        {
            MatchJoinModel matchJoinModel = this.dbContext.MatchJoinModel.SingleOrDefault(item => item.Id == id);
            return View(matchJoinModel);
        }

        /// <summary>
        /// �����û�����
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]

        public IActionResult MatchJoinModify(MatchJoinModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Id > 0)
                {
                    MatchJoinModel matchJoinModel =
                        this.dbContext.MatchJoinModel.SingleOrDefault(item => item.Id == model.Id);
                    matchJoinModel.first = model.first;
                    matchJoinModel.second = model.second;
                    matchJoinModel.third = model.third;
                    matchJoinModel.fourth = model.fourth;
                    matchJoinModel.Score = model.Score;
                    matchJoinModel.Rank = model.Rank;
                    this.dbContext.MatchJoinModel.Update(matchJoinModel);
                }
                this.dbContext.SaveChanges();
                return Redirect("/MatchInfo/Index");
            }
            return View(model);
        }

        #endregion
    }
}