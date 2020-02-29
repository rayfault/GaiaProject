using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;

namespace GaiaDbContext.Models.SystemModels
{
    /// <summary>
    /// 뉴스 정보
    /// </summary>
    public class NewsInfoModel
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 직함
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string name { get; set; }
        /// <summary>
        /// 소개
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(50)]

        public string remark { get; set; }
        /// <summary>
        /// 설립자
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string username { get; set; }

        /// <summary>
        /// 상태 삭제
        /// </summary>
        public int isDelete { get; set; }

        /// <summary>
        /// 상태
        /// </summary>
        public int state { get; set; }

        /// <summary>
        /// 내용
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(4000)]
        public string contents { get; set; }
        /// <summary>
        /// 타입
        /// </summary>
        public int type { get; set; }
        /// <summary>
        /// 시간 추가
        /// </summary>
        public DateTime? AddTime { get; set; }

        /// <summary>
        /// 정렬
        /// </summary>
        public int Rank { get; set; }
    }
}