namespace Gaia.Model.Match
{
    /// <summary>
    /// 경쟁 사용자 통계
    /// </summary>
    public class MatchUserStatistics
    {
        /// <summary>
        /// 아이디
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// 참여 수
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 총점
        /// </summary>
        public int ScoreTotal { get; set; }
        /// <summary>
        /// 평균 점수
        /// </summary>
        public float ScoreAvg { get; set; }
    }
}