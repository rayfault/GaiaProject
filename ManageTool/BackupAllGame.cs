using GaiaCore.Gaia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GaiaProject.Data;
using Microsoft.EntityFrameworkCore;

namespace ManageTool
{
    public class BackupAllGame : Daemon
    {
        protected override int m_timeOut { get => 300 * 1000; }

        public override void InvokeAction()
        {
            //DbContextOptions<ApplicationDbContext> dbContextOptions=new DbContextOptions<ApplicationDbContext>();
            //ApplicationDbContext dbContext=new ApplicationDbContext(options: dbContextOptions);

            GameMgr.BackupDictionary();
            GameMgr.RemoveOldBackupData();
        }
    }
}

