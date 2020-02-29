using GaiaDbContext.Models;
using GaiaDbContext.Models.AccountViewModels;
using GaiaDbContext.Models.HomeViewModels;
using GaiaDbContext.Models.SystemModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GaiaProject.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        /// <summary>
        /// 결과 저장 여부
        /// </summary>
        public const bool isSaveResult = true;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);
        }

        public DbSet<UserFriend> UserFriend { get; set; }

        /// <summary>
        /// 게임 정보
        /// </summary>
        public DbSet<GameInfoModel> GameInfoModel { get; set; }

        /// <summary>
        /// 게이머 레이스 정보
        /// </summary>
        public DbSet<GameFactionModel> GameFactionModel { get; set; }

        /// <summary>
        /// 인종 확장 정보
        /// </summary>
        public DbSet<GameFactionExtendModel> GameFactionExtendModel { get; set; }

        /// <summary>
        /// 게임 요청 삭제
        /// </summary>
        public DbSet<GameDeleteModel> GameDeleteModel { get; set; }

        /// <summary>
        /// 경기 정보
        /// </summary>
        public DbSet<MatchInfoModel> MatchInfoModel { get; set; }

        /// <summary>
        /// 경쟁에 참여
        /// </summary>
        public DbSet<MatchJoinModel> MatchJoinModel { get; set; }


        /// <summary>
        /// 뉴스
        /// </summary>
        public DbSet<NewsInfoModel> NewsInfoModel { get; set; }
    }
}
