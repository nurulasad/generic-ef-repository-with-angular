using GenericRepository.Model;
using System;
using System.Collections.Generic;

namespace GenericRepository.Model.Plain
{
    
    public class ProductModel
    {
        public long Id { get; set; }
        public string ProductName { get; set; }
        public long? SupplierId { get; set; }
        public decimal? UnitPrice { get; set; }
        public string Package { get; set; }
        public bool IsDiscontinued { get; set; }
        public string ProductType { get; set; }
        
        public string Created { get; set; }
        public string CreatedBy { get; set; }

        public string Updated { get; set; }
        public string UpdatedBy { get; set; }


        
        


        [Obsolete]
        public ProductModel()
        {

        }
    }

    


}
