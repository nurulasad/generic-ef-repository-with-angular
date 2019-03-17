using Database.EFModel;
using GenericRepository.Model;
using GenericRepository.Model.Id;
using GenericRepository.Model.Plain;
using log4net;
using MyUtility;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using MyUtility.Extensions;

namespace DatabaseLayer.Core
{

    public abstract class DALBase<TIn, TDbObj> 
            where TIn : IInfoObject
            where TDbObj : class
    {
        protected ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string ConnectionKey;


        public virtual List<TIn> GetAll()
        {
            List<TDbObj> items = null;
            using (DbContext db = new MyCoreContainer(ConnectionKey))
            {
                items = db.Set<TDbObj>().ToList();
            }

            return ToInfo(items);
        }

        public TIn Get(long id)
        {
            TDbObj item = default(TDbObj);
            
            using (DbContext db = new MyCoreContainer(ConnectionKey))
            {
                item = db.Set<TDbObj>().Find(id);
            }

            return ToInfo(item);
        }

        public virtual DataTablePagination<TIn> GetListPaged(string searchValue, string orderByColumn, string orderByDirection
            , int skipCount, int pageSize, List<string> searchInColumns = null)
        {

            bool orderByDesc = true;
            if (string.IsNullOrEmpty(orderByDirection) || orderByDirection.ToLower() == "asc")
            {
                orderByDesc = false;
            }

            int totalItems = 0;
            int totalFiltered = 0;

            orderByColumn = GetOrderByColumn(orderByColumn);
            
            List<TIn> items = new List<TIn>();
            using (DbContext db = new MyCoreContainer(ConnectionKey))
            {

                totalItems = db.Set<TDbObj>().Count();

                IQueryable<TDbObj> query = db.Set<TDbObj>()
                                   .Where(GetWhereClause(searchInColumns, searchValue));

                totalFiltered = query.Count();


                List<TDbObj> filteredDbItems = query
                        .MyOrderBy(orderByColumn.ToString(), orderByDesc)
                        .Skip(skipCount)
                        .Take(pageSize).ToList();


                items = ToInfo(filteredDbItems);
            }

            DataTablePagination<TIn> paged = GetPagination(totalItems, totalFiltered, items);


            return paged;



        }

        public virtual Expression<Func<TDbObj, bool>> GetWhereClause(List<string> searchInColumns, string searchValue)
        {
            List<ColumnDefinition> columnDefinitions = new List<ColumnDefinition>();
            PropertyInfo[] pInfos = MyConverterFactory.GetProperties<TIn>();

            if (searchInColumns != null && searchInColumns.Count > 0)
            {
                pInfos = pInfos.Where(x => searchInColumns.Any(y => y == x.Name)).ToArray();
            }

            foreach (PropertyInfo pInfo in pInfos)
            {
                columnDefinitions.Add(new ColumnDefinition() { Name = pInfo.Name, Type = pInfo.PropertyType });

            }
            return MyEntitySearchExpression.GetExpression<TDbObj>(columnDefinitions, searchValue);
        }



        private DataTablePagination<TIn> GetPagination(int totalItems, int totalFiltered, List<TIn> items)
        {
            DataTablePagination<TIn> paged = new DataTablePagination<TIn>()
            {
                RecordsTotal = totalItems,
                RecordsFiltered = totalFiltered,

                Data = items
            };

            return paged;

        }

        private string GetOrderByColumn(string orderByColumn)
        {
            if (string.IsNullOrEmpty(orderByColumn))
            {

                PropertyInfo pInfo = typeof(TIn).GetProperty("Id");
                if (pInfo != null)
                {
                    orderByColumn = "Id";
                }
                else
                {
                    PropertyInfo[] pInfos = MyConverterFactory.GetProperties<TIn>();

                    if (pInfos.Count() > 0)
                    {
                        orderByColumn = pInfos.First().Name;
                    }
                    else
                    {
                        throw new Exception("No column found for default ordering. Entity = " + typeof(TIn));
                    }

                }
            }
            return orderByColumn;
        }



        public virtual void Save(TIn info)
        {

            using (MyCoreContainer db = new MyCoreContainer(ConnectionKey))
            {
                MyConverter<TIn, TDbObj> mGConverter = MyConverterFactory.GetConverter<TIn, TDbObj>();

                TDbObj dbItem = null;

                PropertyInfo pInfo = info.GetType().GetProperty("Id");
                object id = pInfo.GetValue(info);

                if (id == null || long.Parse(id.ToString()) == 0)
                {
                    dbItem = mGConverter.ConvertToModel(info);
                    db.Set<TDbObj>().Add(dbItem);

                }
                else
                {
                    dbItem = db.Set<TDbObj>().Find(long.Parse(id.ToString()));
                    db.Set<TDbObj>().Attach(dbItem);
                }

                BeforeSave(info, dbItem);

                db.Entry(dbItem).Property("Created").IsModified = false;
                db.Entry(dbItem).Property("CreatedBy").IsModified = false;
                SaveDbChange(db);

                PropertyInfo pdbInfo = dbItem.GetType().GetProperty("Id");
                object dbId = pdbInfo.GetValue(dbItem);


                object property = Activator.CreateInstance(pInfo.PropertyType, pdbInfo.GetValue(dbItem));
                pInfo.SetValue(info, property);

            }
        }
        public virtual void Save(List<TIn> infos)
        {
            MyConverter<TIn, TDbObj> mGConverter = MyConverterFactory.GetConverter<TIn, TDbObj>();

            using (MyCoreContainer db = new MyCoreContainer(ConnectionKey))
            {
                foreach (TIn info in infos)
                {

                    TDbObj dbItem = null;

                    PropertyInfo pInfo = info.GetType().GetProperty("Id");
                    object id = pInfo.GetValue(info);

                    if (id == null || long.Parse(id.ToString()) == 0)
                    {
                        dbItem = mGConverter.ConvertToModel(info);
                        db.Set<TDbObj>().Add(dbItem);

                    }
                    else
                    {
                        dbItem = db.Set<TDbObj>().Find(long.Parse(id.ToString()));
                        db.Set<TDbObj>().Attach(dbItem);
                    }

                    BeforeSave(info, dbItem);
                    db.Entry(dbItem).Property("Created").IsModified = false;
                    db.Entry(dbItem).Property("CreatedBy").IsModified = false;


                }

                SaveDbChange(db);
            }

        }
        protected virtual void BeforeSave(TIn info, TDbObj dbItem)
        {
            //Validate(info);
            PopulateSameTypeProperty(info, dbItem);
        }


        protected virtual void SaveDbChange(DbContext db)
        {
            try
            {
                db.SaveChanges();
            }
            catch (DbEntityValidationException ex)
            {
                log.Error(ex.Message, ex);
                MessageInfo msg = new MessageInfo();

                foreach (DbEntityValidationResult ve in ex.EntityValidationErrors)
                {
                    foreach (DbValidationError err in ve.ValidationErrors)
                    {
                        msg.Error.Add(err.ErrorMessage);
                    }
                }

                throw new EndUserFriendlyException(ex.Message, ex, msg);

            }
            catch (SqlException ex)
            {

                log.Error(ex.Message, ex);
                throw;

            }
            catch (Exception ex)
            {

                log.Error(ex.Message, ex);
                log.Error(ex.GetBaseException().Message);

                throw;

            }
        }


        private TIn PopulateSameTypeProperty(TIn info, TDbObj dbItem)
        {

            try
            {
                PropertyInfo[] pInfos = MyConverterFactory.GetProperties<TIn>();


                foreach (PropertyInfo pInfo in pInfos)
                {

                    PropertyInfo targetPropertyInfo = dbItem.GetType().GetProperty(pInfo.Name);
                    if (targetPropertyInfo == null)
                        continue;
                    
                    if(pInfo.PropertyType == typeof(DateTime) && pInfo.Name == "Updated")
                    {
                        targetPropertyInfo.SetValue(dbItem, DateTime.UtcNow);
                    }
                    else if (pInfo.PropertyType == typeof(DateTime) && pInfo.Name == "Created")
                    {
                        targetPropertyInfo.SetValue(dbItem, DateTime.UtcNow);
                    }
                    else if (pInfo.PropertyType.BaseType == typeof(LongId))
                    {
                        if (pInfo.GetValue(info) == null)
                        {
                            targetPropertyInfo.SetValue(dbItem, null);
                        }
                        else
                        {
                            targetPropertyInfo.SetValue(dbItem, long.Parse(pInfo.GetValue(info).ToString()));
                        }
                    }
                    else if (pInfo.PropertyType.IsEnum || MyHelper.IsNullableEnum(pInfo.PropertyType))
                    {
                        if (pInfo.GetValue(info) == null)
                        {
                            targetPropertyInfo.SetValue(dbItem, null);
                        }
                        else
                        {
                            targetPropertyInfo.SetValue(dbItem, pInfo.GetValue(info).ToString());
                        }
                    }

                    else if (targetPropertyInfo.PropertyType == pInfo.PropertyType)
                    {
                        targetPropertyInfo.SetValue(dbItem, pInfo.GetValue(info));
                    }

                    else
                    {
                        throw new Exception("Unknown property field " + pInfo.PropertyType.FullName);
                    }

                }

                return info;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                throw;

            }

        }


        protected virtual TIn ToInfo(TDbObj dbItem)
        {
            if (dbItem == null)
            {
                return default(TIn);
            }

            MyConverter<TDbObj, TIn> mGConverter = MyConverterFactory.GetConverter<TDbObj, TIn>();
            return mGConverter.ConvertToInfo(dbItem);
        }
        protected virtual List<TIn> ToInfo(List<TDbObj> dbItems)
        {
            List<TIn> infos = new List<TIn>();
            foreach (TDbObj dbItem in dbItems)
            {
                infos.Add(ToInfo(dbItem));
            }
            return infos;
        }


        public virtual void Delete(long id)
        {
            using (MyCoreContainer db = new MyCoreContainer(ConnectionKey))
            {

                TDbObj dbItem = db.Set<TDbObj>().Find(long.Parse(id.ToString()));
                db.Set<TDbObj>().Remove(dbItem);
                
                db.SaveChanges();

            }
        }
    }
}


