using Castle.Core;
using Database.EFModel;
using DatabaseLayer.Interfaces.Core;
using GenericRepository.Model;
using MyFramework;

namespace DatabaseLayer.Core
{
    [CastleComponent(typeof(IProductDAL<ProductInfo>))]
    public class ProductDAL : DALBase<ProductInfo, Product>, IProductDAL<ProductInfo>
    {

        public ProductDAL()
        {
            ConnectionKey = Config.CoreContainer;
        }
        

    }
}


