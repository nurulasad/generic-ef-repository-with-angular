using Database.EFModel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database.EFModel
{
    /// <summary>
    /// This partial class is used for any extensions/changes to the autogenerate class "CoreContainer".
    /// This class will not be changed or overwritten when EntityFramework auto generates classes.
    /// </summary>
    public partial class MyCoreContainer : CoreContainer
    {
        public MyCoreContainer(string connectionString)
            : base(connectionString)
        {

        }
    }
}
