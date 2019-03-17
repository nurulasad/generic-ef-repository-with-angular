using GenericRepository.Model.Id;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace GenericRepository.Model
{

    public class DataTableAjaxPostModel
    {
        // properties are not capital due to json mapping
        public int draw { get; set; }
        public int start { get; set; }
        public int length { get; set; }
        public string orderByColumnName { get; set; }
        public string orderByDirection { get; set; }
        public string search { get; set; }

        public List<string> columns { get; set; }

    }

}