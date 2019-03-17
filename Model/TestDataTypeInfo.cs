using GenericRepository.Model.Id;
using GenericRepository.Model.Plain;
using System;

namespace GenericRepository.Model
{

    public class TestDataTypeInfo : IInfoObject
    {
        public DataTypeId Id { get; set; }
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


        public ServiceType Enum { get; set; }
        public ServiceType? EnumNullable { get; set; }

        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    
        public DateTime Updated { get; set; }
        public string UpdatedBy { get; set; }


        [Obsolete]
        public TestDataTypeInfo()
        {

        }

        public TestDataTypeInfo(DataTypeId id, DateTime created, string createdBy , DateTime updated, string updatedBy)
        {
            Id = id;
            Created = created;
            Updated = updated;
            UpdatedBy = updatedBy;
        }





    }
}
