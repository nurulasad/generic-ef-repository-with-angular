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
    [CastleComponent(typeof(IProductBLL<ProductInfo, ProductId>))]
    public class ProductBLL : BLLBase<ProductInfo, ProductId>, IProductBLL<ProductInfo, ProductId>
    {

        private IProductDAL<ProductInfo> _dal = null;
        IProductDAL<ProductInfo> dal { get { return _dal ?? (_dal = new ProductDAL()); } }
        
        public ProductBLL()
        {
            Dal = dal;
        }

    }
}
