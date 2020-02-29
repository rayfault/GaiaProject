using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace GaiaDbContext.Models.AccountViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UserGameModel
    {
        public UserGameModel()
        {
            this.isTishi = true;
            this.resetNumber = 5;
            this.resetPayNumber = 5;
        }
        /// <summary>
        /// 아이디
        /// </summary>
        [JsonProperty]
        public string username { get; set; }
        /// <summary>
        /// 메모
        /// </summary>
        [JsonProperty]
        public string remark { get; set; }
        /// <summary>
        /// 정보를 프롬프트할지 여부
        /// </summary>
        [JsonProperty]
        public bool isTishi { get; set; }
        /// <summary>
        /// 자동 새로 고침
        /// </summary>
        [JsonProperty]

        public bool isSocket { get; set; }

        /// <summary>
        /// 정교화 재설정
        /// </summary>

        [JsonProperty]

        public int resetNumber { get; set; }

        /// <summary>
        /// 회원 강제 반품
        /// </summary>

        [JsonProperty]

        public int resetPayNumber { get; set; }

        /// <summary>
        /// 지불 수준
        /// </summary>
        [JsonProperty]
        public int? paygrade { get; set; }


        /// <summary>
        /// 사용자가 참여한 시점
        /// </summary>
        public int scoreUserStart { get; set; }



    }
}
