using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GaiaDbContext.Models.HomeViewModels
{
    /// <summary>
    /// 표시 할 세부 사항 일치
    /// </summary>
    public class MatchShowModel
    {
        public MatchInfoModel MatchInfoModel { get; set; }

        public IQueryable<MatchJoinModel> MatchJoinModels { get; set; }

        public IQueryable<GameInfoModel> GameInfoModels { get; set; }
    }
}
