
using Database.EFModel;
using GenericRepository.Model;
using GenericRepository.Model.Id;
using GenericRepository.Model.Plain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyUtility;
using System;

namespace DAL.Core.Test
{
    [TestClass]
    public class ModelConverterTestCase : BaseDALTestCase
    {
        [TestMethod]
        public void Generic_Info_ToModel_Converter_Test()
        {
            using (CreateTransaction())
            {

                TestDataTypeInfo info = new TestDataTypeInfo(null, DateTime.Now, "test", DateTime.Now, "test");

                info.String = "name";

                info.Bit = true;
                info.BitNullable = null;

                info.Decimal = decimal.MinValue + 1;
                info.DecimalNullable = null;

                info.Enum = ServiceType.PostPaid;
                info.EnumNullable = null;

                info.Integer = int.MinValue + 2;
                info.IntegerNullable = null;

                info.Long = long.MinValue + 3;
                info.LongNullable = null;

                info.Short = short.MinValue + 4;
                info.ShortNullable = null;

                MyConverter<TestDataTypeInfo, TestDataTypeModel> myConverter = new MyConverter<TestDataTypeInfo, TestDataTypeModel>();


                TestDataTypeModel model = myConverter.ConvertToModel(info);


                Assert.AreEqual("name", model.String);
                Assert.AreEqual(true, model.Bit);
                Assert.AreEqual(decimal.MinValue + 1, model.Decimal);
                Assert.AreEqual("PostPaid", model.Enum);
                Assert.AreEqual(int.MinValue + 2, model.Integer);
                Assert.AreEqual(long.MinValue + 3, model.Long);
                Assert.AreEqual(short.MinValue + 4, model.Short);

                Assert.IsNull(model.BitNullable);
                Assert.IsNull(model.DecimalNullable);
                Assert.IsNull(model.EnumNullable);
                Assert.IsNull(model.IntegerNullable);
                Assert.IsNull(model.LongNullable);
                Assert.IsNull(model.ShortNullable);

                //check nullable
                info.BitNullable = true;

                info.DecimalNullable = decimal.MinValue + 1;

                info.EnumNullable = ServiceType.PostPaid;
                info.IntegerNullable = int.MinValue + 2;

                info.LongNullable = long.MinValue + 3;
                info.ShortNullable = short.MinValue + 4;

                model = myConverter.ConvertToModel(info);

                Assert.AreEqual(true, model.BitNullable.Value);
                Assert.AreEqual(decimal.MinValue + 1, model.DecimalNullable.Value);
                Assert.AreEqual("PostPaid", model.EnumNullable);
                Assert.AreEqual(int.MinValue + 2, model.IntegerNullable.Value);
                Assert.AreEqual(long.MinValue + 3, model.LongNullable.Value);
                Assert.AreEqual(short.MinValue + 4, model.ShortNullable.Value);




            }

        }


        [TestMethod]
        public void Generic_Model_ToInfo_Converter_Test()
        {
            using (CreateTransaction())
            {

                TestDataTypeModel model = new TestDataTypeModel();

                model.Id = 1;
                model.String = "name";

                model.Bit = true;
                model.BitNullable = null;

                model.Decimal = decimal.MinValue + 1;
                model.DecimalNullable = null;

                model.Enum = ServiceType.PostPaid.ToString();
                model.EnumNullable = null;

                model.Integer = int.MinValue + 2;
                model.IntegerNullable = null;

                model.Long = long.MinValue + 3;
                model.LongNullable = null;

                model.Short = short.MinValue + 4;
                model.ShortNullable = null;

                MyConverter<TestDataTypeModel, TestDataTypeInfo> myConverter = new MyConverter<TestDataTypeModel, TestDataTypeInfo>();


                TestDataTypeInfo info = myConverter.ConvertToInfo(model);


                Assert.AreEqual("name", info.String);
                Assert.AreEqual(true, info.Bit);
                Assert.AreEqual(decimal.MinValue + 1, info.Decimal);
                Assert.AreEqual(ServiceType.PostPaid, info.Enum);
                Assert.AreEqual(int.MinValue + 2, info.Integer);
                Assert.AreEqual(long.MinValue + 3, info.Long);
                Assert.AreEqual(short.MinValue + 4, info.Short);

                Assert.IsNull(info.BitNullable);
                Assert.IsNull(info.DecimalNullable);
                Assert.IsNull(info.EnumNullable);
                Assert.IsNull(info.IntegerNullable);
                Assert.IsNull(info.LongNullable);
                Assert.IsNull(info.ShortNullable);

                //check nullable
                model.BitNullable = true;

                model.DecimalNullable = decimal.MinValue + 1;

                model.EnumNullable = ServiceType.PostPaid.ToString();
                model.IntegerNullable = int.MinValue + 2;

                model.LongNullable = long.MinValue + 3;
                model.ShortNullable = short.MinValue + 4;

                info = myConverter.ConvertToInfo(model);

                Assert.AreEqual(true, info.BitNullable.Value);
                Assert.AreEqual(decimal.MinValue + 1, info.DecimalNullable.Value);
                Assert.AreEqual(ServiceType.PostPaid, info.EnumNullable);
                Assert.AreEqual(int.MinValue + 2, info.IntegerNullable.Value);
                Assert.AreEqual(long.MinValue + 3, info.LongNullable.Value);
                Assert.AreEqual(short.MinValue + 4, info.ShortNullable.Value);




            }

        }
    }
}
