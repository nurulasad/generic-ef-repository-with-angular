using GenericRepository.Model;
using System;
using System.Collections.Generic;

namespace GenericRepository.Model.Plain
{
    public class DataTypeData
    {
        public List<DataTypeModel> DataTypes { get; set; }
       
        public DataTypeData()
        {
            DataTypes = new List<DataTypeModel>();
        }
    }

    public class DataTypeModel
    {
        public long Id { get; set; }
        public bool Bit { get; set; }
        public decimal? Decimal { get; set; }
        public int? Integer { get; set; }
        public decimal? Money { get; set; }
        public decimal? Numeric { get; set; }
        public short? Smallint { get; set; }

        public string ServiceType { get; set; }
        public string Created { get; set; }
        public string CreatedBy { get; set; }

        public string Updated { get; set; }
        public string UpdatedBy { get; set; }

        [Obsolete]
        public DataTypeModel()
        {

        }
    }

    


}
