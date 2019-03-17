using BusinessLayer.Interfaces.Core;
using Castle.Core;
using DatabaseLayer.Core;
using DatabaseLayer.Interfaces.Core;
using GenericRepository.Model;
using GenericRepository.Model.Id;
using log4net;
using System.Collections.Generic;
using System.Reflection;

namespace BusinessLayer.Core
{
    [CastleComponent(typeof(IDataTypeBLL<DataTypeInfo, DataTypeId>))]
    public class DataTypeBLL : BLLBase<DataTypeInfo, DataTypeId>, IDataTypeBLL<DataTypeInfo, DataTypeId>
    {
        
        private IDataTypeDAL<DataTypeInfo> _dal = null;
        IDataTypeDAL<DataTypeInfo> dal { get { return _dal ?? (_dal = new DataTypeDAL()); } }
        
        public DataTypeBLL()
        {
            Dal = dal;
        }

    }
}
