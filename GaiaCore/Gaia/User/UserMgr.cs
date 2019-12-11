using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace GaiaCore.Gaia.User
{
    public static class PowerUser
    {
        public static List<string> PowerUserList = new List<string>()
        {
            "RRR",
        };
        public static bool IsPowerUser(string username)
        { 
            return PowerUserList.Contains(username);
        }

    }
}
