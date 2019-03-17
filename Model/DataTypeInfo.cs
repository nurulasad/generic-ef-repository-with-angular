using GenericRepository.Model.Id;
using GenericRepository.Model.Plain;
using System;

namespace GenericRepository.Model
{

    public class DataTypeInfo : IInfoObject
    {
        public DataTypeId Id { get; set; }
        public string Name { get; set; }
        public bool Bit { get; set; }
        public decimal? Decimal { get; set; }
        public int? Integer { get; set; }
        public decimal? Money { get; set; }
        public decimal? Numeric { get; set; }
        public short? Smallint { get; set; }

        public ServiceType Enum { get; set; }
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; }
    
        public DateTime Updated { get; set; }
        public string UpdatedBy { get; set; }


        [Obsolete]
        public DataTypeInfo()
        {

        }

        public DataTypeInfo(DataTypeId id, string name, bool boolean, decimal? dec, int? integer, decimal? money, decimal? numeric, short? smallint
            , ServiceType enumeration, DateTime created,  string createdBy, DateTime updated, string updatedBy )
        {
            Id = id;
            Name = name;
            Bit = boolean;
            Decimal = dec;
            Integer = integer;
            Money = money;
            Numeric = numeric;
            Smallint = smallint;
            Enum = enumeration;
            Created = created;
            CreatedBy = createdBy;
            Updated = updated;
            UpdatedBy = updatedBy;
        }

        

    }
}
