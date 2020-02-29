using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace GaiaCore.Gaia.User
{
    public static class PowerUser
    {
        private static readonly List<string> PowerUserList = new List<string>()
        {
            "RRR",
        };
        public static bool IsPowerUser(string username)
        { 
            return PowerUserList.Contains(username);
        }
    }
}
