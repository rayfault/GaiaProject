// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace GaiaProject.Models.Data
{
    public partial class GameInfoController
    {
        public class StatisticsFaction
        {
            /// <summary>
            /// 이름
            /// </summary>
            public string ChineseName { get; set; }
            /// <summary>
            /// 라운드
            /// </summary>
            public int count { get; set; }
            /// <summary>
            /// 최저 점수
            /// </summary>
            public int scoremin { get; set; }

            /// <summary>
            /// 최고 점수
            /// </summary>
            public int scoremax { get; set; }

            public string scoremaxuser { get; set; }
            /// <summary>
            /// 평균 점수
            /// </summary>
            public int scoreavg { get; set; }
            /// <summary>
            /// 승률
            /// </summary>
            public int winprobability { get; set; }
            /// <summary>
            /// 승리
            /// </summary>
            public int numberwin { get; set; }

            /// <summary>
            /// 외관 비율
            /// </summary>
            public int OccurrenceRate { get; set; }

        }

    }
}
