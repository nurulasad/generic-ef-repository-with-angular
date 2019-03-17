using GenericRepository.Model;
using System.Collections.Generic;

namespace DatabaseLayer.Interfaces.Core
{
    public interface IDALCommon<TIn>
    {
        TIn Get(long id);
        List<TIn> GetAll();

        DataTablePagination<TIn> GetListPaged(string searchValue, string orderByColumn, string orderByDirection
            , int skipPage, int pageSize, List<string> searchInColumns = null);

        void Save(TIn info);
        void Save(List<TIn> infos);

        void Delete(long id);




    }
}
