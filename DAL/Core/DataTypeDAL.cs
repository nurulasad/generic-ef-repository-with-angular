using Castle.Core;
using Database.EFModel;
using DatabaseLayer.Interfaces.Core;
using GenericRepository.Model;
using MyFramework;

namespace DatabaseLayer.Core
{
    [CastleComponent(typeof(IDataTypeDAL<DataTypeInfo>))]
    public class DataTypeDAL : DALBase<DataTypeInfo, DataType>, IDataTypeDAL<DataTypeInfo>
    {

        public DataTypeDAL()
        {
            ConnectionKey = Config.CoreContainer;
        }
        

    }
}


