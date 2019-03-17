using System;

namespace GenericRepository.Model.Id
{
    [Serializable]
    public class DataTypeId : LongId
    {
        public DataTypeId() { }
        public DataTypeId(long id) : base(id) { }
    }

    [Serializable]
    public class ProductId : LongId
    {
        public ProductId() { }
        public ProductId(long id) : base(id) { }
    }





}