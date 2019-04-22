using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GaiaDbContext.Models;
using GaiaDbContext.Models.SystemModels;
using GaiaProject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaProject.Controllers
{
    public class AdminController : BaseControlNews
    {
        private readonly ApplicationDbContext dbContext;
        private IMemoryCache cache;

        public AdminController(ApplicationDbContext dbContext, IMemoryCache cache):base(dbContext)
        {
            this.dbContext = dbContext;
            this.cache = cache;
        }

        public IActionResult Index()
        {
            return View();
        }

        #region ����

        /// <summary>
        /// �����б�
        /// </summary>
        /// <returns></returns>

        public IActionResult NewsIndex()
        {
            List<NewsInfoModel> newsInfoModels = this.dbContext.NewsInfoModel.ToList();
            return View(newsInfoModels);
        }
        /// <summary>
        /// ��������
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult NewsUpdate(int? id)
        {
            NewsInfoModel newModel = base.News_Update(id);
            return View(newModel);
        }
        /// <summary>
        /// �ύ����
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult NewsUpdate(NewsInfoModel model)
        {
            NewsInfoModel newModel = base.News_Update(model);
            return Redirect("/Admin/NewsIndex");
        }

        public IActionResult NewsIndexUpdate()
        {
            this.cache.Remove(HomeController.IndexName);
            return Redirect("/Admin/NewsIndex");
        }
        #endregion



        #region �ڴ�
        /// <summary>
        /// �б�
        /// </summary>
        /// <returns></returns>
        public IActionResult DonateIndex()
        {
            List<DonateRecordModel> donateRecordModels = this.dbContext.DonateRecordModel.ToList();
            return View(donateRecordModels);
        }
        /// <summary>
        /// ���
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet]       
        public IActionResult DonateUpdate(int? id)
        {
            DonateRecordModel model = null;
            if (id > 0)
            {
                model = this.dbContext.DonateRecordModel.SingleOrDefault(item => item.id == id);
            }
            //List<DonateRecordModel> donateRecordModels = this.dbContext.DonateRecordModel.ToList();
            return View(model);
        }

        [HttpPost]
        public IActionResult DonateUpdate(DonateRecordModel model)
        {
            DonateRecordModel newModel;
            //�༭
            if (model.id > 0)
            {
                newModel = this.dbContext.DonateRecordModel.SingleOrDefault(item => item.id == model.id);
            }
            else//���
            {
                newModel = new DonateRecordModel();
                newModel.addtime = DateTime.Now;
            }
            //��ֵ
            newModel.donateuser = model.donateuser;
            newModel.donateprice = model.donateprice;
            newModel.donatetime = model.donatetime;
            newModel.donatetype = model.donatetype;
            newModel.chequeuser = model.chequeuser;
            newModel.newid = model.newid;
            //newModel.name = model.name;
            //����
            if (model.id > 0)
            {
                this.dbContext.DonateRecordModel.Update(newModel);
            }
            else
            {
                this.dbContext.DonateRecordModel.Add(newModel);
            }

            //�޸��û��ĵȼ�
            ApplicationUser singleOrDefault = this.dbContext.Users.SingleOrDefault(item => item.UserName == newModel.donateuser);
            if (singleOrDefault == null)
            {
                throw new Exception("�Ҳ������û�");
            }
            else
            {
                //�����û���Ϣ
                singleOrDefault.paygrade = 1;
                this.dbContext.Users.Update(singleOrDefault);
            }


            this.dbContext.SaveChanges();
            return Redirect("/Admin/DonateIndex");
        }
        #endregion

        /// <summary>
        /// ɾ���ܽ�
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<JsonResult> DelData(int id,string type)
        {
            Models.Data.UserFriendController.JsonData jsonData = new Models.Data.UserFriendController.JsonData();

            jsonData.info.state = 200;
            switch (type)
            {
                case "donate":
                    DonateRecordModel donateRecordModel = this.dbContext.DonateRecordModel.SingleOrDefault(item => item.id == id);
                    if (donateRecordModel != null)
                    {
                        this.dbContext.DonateRecordModel.Remove(donateRecordModel);
                        this.dbContext.SaveChanges();
                        //��ѯ������¼�Ƿ�Ϊ��
                        if (!this.dbContext.DonateRecordModel.Any(
                            item => item.donateuser == donateRecordModel.donateuser))
                        {
                            //ȡ���û��ȼ�
                            ApplicationUser applicationUser = this.dbContext.Users.SingleOrDefault(item=>item.UserName==donateRecordModel.donateuser);
                            if (applicationUser != null)
                            {
                                applicationUser.paygrade = 0;
                                this.dbContext.Users.Update(applicationUser);
                                this.dbContext.SaveChanges();
                            }
                        }

                    }
                    break;
            }
            return new JsonResult(jsonData);

        }

    }
}