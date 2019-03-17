using GenericRepository.Model;
using System;
using System.Collections.Generic;

namespace GenericRepository.Model.Plain
{

    public class TestDataTypeModel
    {
        public long Id { get; set; }
        public string String { get; set; }

        public bool Bit { get; set; }
        public bool? BitNullable { get; set; }

        public decimal Decimal { get; set; }
        public decimal? DecimalNullable { get; set; }

        public int Integer { get; set; }
        public int? IntegerNullable { get; set; }


        public long Long { get; set; }
        public long? LongNullable { get; set; }

        public short Short { get; set; }
        public short? ShortNullable { get; set; }


        public string Enum { get; set; }
        public string EnumNullable { get; set; }

      
        public string Created { get; set; }
        public string CreatedBy { get; set; }

        public string Updated { get; set; }
        public string UpdatedBy { get; set; }

        [Obsolete]
        public TestDataTypeModel()
        {

        }
    }

    


}
