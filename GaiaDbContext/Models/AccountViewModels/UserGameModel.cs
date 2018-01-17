﻿using System;
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
        }
        /// <summary>
        /// 用户名
        /// </summary>
        [JsonProperty]
        public string username { get; set; }
        /// <summary>
        /// 备忘
        /// </summary>
        [JsonProperty]
        public string remark { get; set; }
        /// <summary>
        /// 是否提示信息
        /// </summary>
        [JsonProperty]
        public bool isTishi { get; set; }
    }
}