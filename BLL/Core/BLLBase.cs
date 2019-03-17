using DatabaseLayer.Interfaces.Core;
using GenericRepository.Model;
using GenericRepository.Model.Id;
using System.Collections.Generic;

namespace BusinessLayer.Core
{
    public abstract class BLLBase<TInfo, TId>
           where TInfo : IInfoObject
           where TId : LongId
    {

        public BLLBase()
        {

        }

        private IDALCommon<TInfo> _dal = null;
        protected IDALCommon<TInfo> Dal { get { return _dal; } set { _dal = value; } }


        public TInfo Get(TId id)
        {
            return Dal.Get(id.Value);
        }

        public List<TInfo> GetAll()
        {
            return Dal.GetAll();
        }

        public void Save(TInfo info)
        {
            Dal.Save(info);
        }

        public void Delete(TId id)
        {
            Dal.Delete(id.Value);
        }

        public DataTablePagination<TInfo> GetListPaged(string searchValue, string orderByColumn, string orderByDirection, int skipPage, int pageSize, List<string> searchInColumns = null)
        {
            return Dal.GetListPaged(searchValue, orderByColumn, orderByDirection, skipPage, pageSize, searchInColumns);
        }
    }
}
