using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace GaiaDbContext.Models.AccountViewModels
{
    public class UserFriend
    {
        [Key]
        public int Id { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string UserName { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string UserId { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string UserNameTo { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string UserIdTo { get; set; }
        /// <summary>
        /// 참고
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string Remark { get; set; }

        /// <summary>
        /// 친구 유형，1=화이트리스트，2=블랙리스트
        /// </summary>
        public int Type { get; set; }
    }
}
