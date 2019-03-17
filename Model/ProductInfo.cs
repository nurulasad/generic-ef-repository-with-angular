using GenericRepository.Model.Id;
using GenericRepository.Model.Plain;
using System;

namespace GenericRepository.Model
{

    public class ProductInfo : IInfoObject
    {
        public ProductId Id { get; set; }
        public string ProductName { get; set; }
        public long? SupplierId { get; set; }
        public decimal? UnitPrice { get; set; }
        public string Package { get; set; }
        public bool IsDiscontinued { get; set; }
        public string ProductType { get; set; }
        
        public System.DateTime Created { get; set; }
        public string CreatedBy { get; set; }
        public System.DateTime Updated { get; set; }
        public string UpdatedBy { get; set; }


        [Obsolete]
        public ProductInfo()
        {

        }
        

    }
}
