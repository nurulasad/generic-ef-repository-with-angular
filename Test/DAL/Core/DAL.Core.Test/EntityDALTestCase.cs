using DatabaseLayer.Core;
using DatabaseLayer.Interfaces.Core;
using GenericRepository.Model;
using GenericRepository.Model.Plain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyUtility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DAL.Core.Test
{
    [TestClass]
    public class EntityDALTestCase : BaseDALTestCase
    {
        

        [TestMethod]
        public void Generic_Operation_Test()
        {
            using (CreateTransaction())
            {

                IDataTypeDAL<DataTypeInfo> dal = DALContainer.Instance.Resolve<IDataTypeDAL<DataTypeInfo>>();

                DataTypeInfo info = new DataTypeInfo(null, "name", true, null,null, null, null, null, ServiceType.PostPaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                
                //save new
                dal.Save(info);
                DataTypeInfo loaded = dal.Get(info.Id.Value);
                Assert.IsTrue(loaded.Id.Value > 0);
                Assert.AreEqual("name", loaded.Name);

                loaded.Name = "name1";
                loaded.Money = 350m;
                //update 
                dal.Save(loaded);
                loaded = dal.Get(loaded.Id.Value);
                Assert.AreEqual("name1", loaded.Name);
                Assert.AreEqual(350m, loaded.Money);


                dal.Delete(loaded.Id.Value);
                DataTypeInfo deletedInfo = dal.Get(loaded.Id.Value);
                Assert.IsNull(deletedInfo);

                //list save
                string uniqueName = Guid.NewGuid().ToString();
                List<DataTypeInfo> infos = new List<DataTypeInfo>();
                for (int i = 0; i < 6; i++)
                {
                    info = new DataTypeInfo(null, uniqueName+"_name_" +i, true, null, null, null, null, null, ServiceType.PostPaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                    infos.Add(info);

                }

                //save new list
                dal.Save(infos);
                List<DataTypeInfo> loadeds = dal.GetAll().Where(x=>x.Name.StartsWith(uniqueName)).ToList();

                Assert.IsTrue(loadeds.Count ==  6);

                //update
                dal.Save(loadeds);


            }

        }


        [TestMethod]
        public void Generic_All_dataType_Test()
        {
            using (CreateTransaction())
            {

                IDataTypeDAL<DataTypeInfo> dal = DALContainer.Instance.Resolve<IDataTypeDAL<DataTypeInfo>>();

                DataTypeInfo info = new DataTypeInfo(null, "name", true, null, null, null, null, null, ServiceType.PostPaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");

                //save new
                dal.Save(info);
                DataTypeInfo loaded = dal.Get(info.Id.Value);
                Assert.IsTrue(loaded.Id.Value > 0);

                Assert.AreEqual("name", loaded.Name);
                Assert.IsTrue(loaded.Bit);
                Assert.IsNull(loaded.Decimal);
                Assert.IsNull(loaded.Integer);
                Assert.IsNull(loaded.Money);
                Assert.IsNull(loaded.Numeric);
                Assert.IsNull(loaded.Smallint);
                Assert.AreEqual(ServiceType.PostPaid, loaded.Enum);

                //update to non null
                info = new DataTypeInfo(loaded.Id, "name1", true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                dal.Save(info);
                loaded = dal.Get(info.Id.Value);
                Assert.AreEqual("name1", loaded.Name);
                Assert.AreEqual(true, loaded.Bit);
                Assert.AreEqual(20m, loaded.Decimal);
                Assert.AreEqual(25, loaded.Integer);
                Assert.AreEqual(30m, loaded.Money);
                Assert.AreEqual(35m, loaded.Numeric);
                Assert.AreEqual((short)11, loaded.Smallint);
                Assert.AreEqual(ServiceType.PrePaid, loaded.Enum);


                //save new with non null
                info = new DataTypeInfo(null, "name2", true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid, 
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                dal.Save(info);
                loaded = dal.Get(info.Id.Value);


                Assert.AreEqual("name2", loaded.Name);
                Assert.AreEqual(true, loaded.Bit);
                Assert.AreEqual(20m, loaded.Decimal);
                Assert.AreEqual(25, loaded.Integer);
                Assert.AreEqual(30m, loaded.Money);
                Assert.AreEqual(35m, loaded.Numeric);
                Assert.AreEqual((short)11, loaded.Smallint);
                Assert.AreEqual(ServiceType.PrePaid, loaded.Enum);

                //update to null
                info = new DataTypeInfo(loaded.Id, "name3", true, null, null, null, null, null, ServiceType.PostPaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");

                dal.Save(info);
                loaded = dal.Get(loaded.Id.Value);
                Assert.AreEqual("name3", loaded.Name);
                Assert.IsTrue(loaded.Bit);
                Assert.IsNull(loaded.Decimal);
                Assert.IsNull(loaded.Integer);
                Assert.IsNull(loaded.Money);
                Assert.IsNull(loaded.Numeric);
                Assert.IsNull(loaded.Smallint);
                Assert.AreEqual(ServiceType.PostPaid, loaded.Enum);
                
            }

        }


        [TestMethod]
        public void Generic_Search_Test()
        {
            using (CreateTransaction())
            {

                IDataTypeDAL<DataTypeInfo> dal = DALContainer.Instance.Resolve<IDataTypeDAL<DataTypeInfo>>();
                string uniqueName = Guid.NewGuid().ToString();

                List<DataTypeInfo> infos = new List<DataTypeInfo>();
                DataTypeInfo info1 = new DataTypeInfo(null, "classA_"+ uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                DataTypeInfo info2 = new DataTypeInfo(null, "classA_101" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PostPaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                DataTypeInfo info3 = new DataTypeInfo(null, "classB_" + uniqueName, true, 20m, 101, 30m, 35m, 11, ServiceType.PrePaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                DataTypeInfo info4 = new DataTypeInfo(null, "classB_101" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PostPaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                DataTypeInfo info5 = new DataTypeInfo(null, "classA_" + uniqueName, true, 20m, 25, 30m, 35m, 1011, ServiceType.PrePaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");

                infos.Add(info1);
                infos.Add(info2);
                infos.Add(info3);
                infos.Add(info4);
                infos.Add(info5);


                dal.Save(infos);

                List<DataTypeInfo> loaded = dal.GetAll().Where(x=>x.Name.EndsWith(uniqueName)).ToList();
                Assert.AreEqual(5, loaded.Count);

                DataTablePagination<DataTypeInfo> paged = dal.GetListPaged("classA_", "Id", "asc", 0, 100);
                Assert.AreEqual(3, paged.Data.Count);

                paged = dal.GetListPaged("101", "Id", "asc", 0, 100);
                Assert.AreEqual(4, paged.Data.Count);

                paged = dal.GetListPaged("PostP", "Id", "asc", 0, 100);
                Assert.AreEqual(2, paged.Data.Count);

                //should load all
                paged = dal.GetListPaged(uniqueName, "Id", "asc", 0, 100);
                Assert.AreEqual(5, paged.Data.Count);

                paged = dal.GetListPaged(null, "Id", "asc", 0, 100);
                Assert.IsTrue(paged.Data.Count >= 5);

            }
        }


        [TestMethod]
        public void Pagination_Test()
        {
            using (CreateTransaction())
            {

                IDataTypeDAL<DataTypeInfo> dal = DALContainer.Instance.Resolve<IDataTypeDAL<DataTypeInfo>>();

                List<DataTypeInfo> infos = new List<DataTypeInfo>();

                string uniqueName = Guid.NewGuid().ToString();
                for (int i = 0; i < 5; i++)
                {
                    DataTypeInfo info = new DataTypeInfo(null, "classA_" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                    infos.Add(info);
                }

                for (int i = 0; i < 10; i++)
                {

                    DataTypeInfo info = new DataTypeInfo(null, "classA_" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PostPaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                    infos.Add(info);

                }

                for (int i = 0; i < 15; i++)
                {

                    DataTypeInfo info = new DataTypeInfo(null, "classB_" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid, DateTime.UtcNow, "classA", DateTime.UtcNow, "u2");
                    infos.Add(info);

                }
                for (int i = 0; i < 20; i++)
                {

                    DataTypeInfo info = new DataTypeInfo(null, "classB_" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PostPaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                    infos.Add(info);

                }

                for (int i = 0; i < 25; i++)
                {
                    DataTypeInfo info = new DataTypeInfo(null, "classZ_" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                    infos.Add(info);
                }

                for (int i = 0; i < 1; i++)
                {
                    DataTypeInfo info = new DataTypeInfo(null, "classZ_" + uniqueName, true, 20m, 25, 30m, 35m, 11, ServiceType.PrePaid, DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");
                    infos.Add(info);
                }

                dal.Save(infos);

                DataTablePagination<DataTypeInfo> paged = dal.GetListPaged(uniqueName, "Id", "asc", 0, int.MaxValue);
                Assert.IsTrue(paged.Data.Count == 76);

                //check filter
                paged = dal.GetListPaged("classA", "Id", "asc", 0, 13);
                Assert.AreEqual(13, paged.Data.Count);
                Assert.AreEqual(30, paged.RecordsFiltered);
                Assert.IsTrue(paged.RecordsTotal >= 76);

                //check pagination
                paged = dal.GetListPaged("classA", "Id", "asc", 26, 13);
                Assert.AreEqual(4, paged.Data.Count);

                paged = dal.GetListPaged("classA", "Id", "asc", 32, 13);
                Assert.AreEqual(0, paged.Data.Count);

                //check ordering 
                DataTablePagination<DataTypeInfo> paged1 = dal.GetListPaged("class", "Name", "asc", 15, 13);
                DataTablePagination<DataTypeInfo> paged2 = dal.GetListPaged("class", "Name", "desc", 15, 13);
                Assert.IsTrue(paged1.Data.First().Name != paged2.Data.First().Name, "first item in different order should not match");


                //check filter with column names
                paged = dal.GetListPaged("classA", "Id", "asc", 0, 13, new List<string>() { "Name" });
                Assert.AreEqual(13, paged.Data.Count);
                Assert.AreEqual(15, paged.RecordsFiltered);

 
                //check filter with invalid column names, no data will be returned
                paged = dal.GetListPaged("classA", "Id", "asc", 0, 13, new List<string>() { "InvalidColumn" });
                Assert.AreEqual(0, paged.Data.Count);
                Assert.AreEqual(0, paged.RecordsFiltered);

            }

        }

        [TestMethod]
        public void Create_Created_Change_on_single_save_Test()
        {
            using (CreateTransaction())
            {

                IDataTypeDAL<DataTypeInfo> dal = DALContainer.Instance.Resolve<IDataTypeDAL<DataTypeInfo>>();

                string uniqueName = Guid.NewGuid().ToString();
                DataTypeInfo info = new DataTypeInfo(null, uniqueName, true, 355.0m, 15, 911.50m, 611.50m, 12, ServiceType.PostPaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");

                dal.Save(info);

                DataTypeInfo loaded = dal.GetAll().Where(x => x.Name == uniqueName).Single();

                Assert.IsTrue(loaded.Created > DateTime.UtcNow.AddMinutes(-1));
                Assert.IsTrue(loaded.Updated > DateTime.UtcNow.AddMinutes(-1));

                Assert.IsTrue(loaded.Created == loaded.Updated, "Created should be same as updated");


                Thread.Sleep(250);

                loaded.CreatedBy = "changed";
                dal.Save(loaded);

                loaded = dal.Get(loaded.Id.Value);

                Assert.IsTrue(loaded.Created > DateTime.UtcNow.AddMinutes(-2));
                Assert.IsTrue(loaded.Updated > DateTime.UtcNow.AddMinutes(-2));
                Assert.IsTrue(loaded.Created != loaded.Updated, "Created should not time changed");
                Assert.AreEqual("u1", loaded.CreatedBy, "CreatedBy should not be changed");


            }
        }

        [TestMethod]
        public void Create_Created_Change_on_List_save_Test()
        {
            using (CreateTransaction())
            {

                IDataTypeDAL<DataTypeInfo> dal = DALContainer.Instance.Resolve<IDataTypeDAL<DataTypeInfo>>();

                string uniqueName = Guid.NewGuid().ToString();
                DataTypeInfo info = new DataTypeInfo(null, uniqueName, true, 355.0m, 15, 911.50m, 611.50m, 12, ServiceType.PostPaid,
                    DateTime.UtcNow, "u1", DateTime.UtcNow, "u2");

                dal.Save(new List<DataTypeInfo>() { info });

                DataTypeInfo loaded = dal.GetAll().Where(x => x.Name == uniqueName).Single();

                Assert.IsTrue(loaded.Created > DateTime.UtcNow.AddMinutes(-1));
                Assert.IsTrue(loaded.Updated > DateTime.UtcNow.AddMinutes(-1));

                Assert.IsTrue(loaded.Created == loaded.Updated, "Created should be same as updated");


                Thread.Sleep(250);

                loaded.CreatedBy = "changed";
                dal.Save(new List<DataTypeInfo>() { loaded });

                loaded = dal.Get(loaded.Id.Value);

                Assert.IsTrue(loaded.Created > DateTime.UtcNow.AddMinutes(-2));
                Assert.IsTrue(loaded.Updated > DateTime.UtcNow.AddMinutes(-2));
                Assert.IsTrue(loaded.Created != loaded.Updated, "Created time should not changed");
                Assert.AreEqual("u1", loaded.CreatedBy, "CreatedBy should not be changed");
                
            }
        }


       





    }
}
