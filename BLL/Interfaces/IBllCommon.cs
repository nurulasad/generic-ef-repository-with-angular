using GenericRepository.Model;
using System.Collections.Generic;

namespace BusinessLayer.Interfaces.Core
{

    public interface IBLLCommon<TInfo,TId>
    {
        TInfo Get(TId id);
        List<TInfo> GetAll();
        void Save(TInfo info);
        void Delete(TId id);

        DataTablePagination<TInfo> GetListPaged(string searchValue, string orderByColumn, string orderByDirection
           , int skipPage, int pageSize, List<string> searchInColumns = null);
    }
    
}
