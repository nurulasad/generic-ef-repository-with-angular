using GenericRepository.Model.Id;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace GenericRepository.Model
{

    public class DataTablePagination<T>
    {
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }        
        public List<T> Data { get; set; }
    }

}