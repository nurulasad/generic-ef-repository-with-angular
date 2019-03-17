using System.Collections.Generic;
using System.Web.Mvc;
using BusinessLayer.Core;
using BusinessLayer.Interfaces.Core;
using GenericRepository.Model;
using GenericRepository.Model.Id;
using GenericRepository.Model.Plain;
using MyUtility;

namespace WebClient.Controllers
{

    public class DataController : Controller
    {        

        private IProductBLL<ProductInfo, ProductId> _dataTypeBll = null;
        protected IProductBLL<ProductInfo, ProductId> DataTypeBll { get { return _dataTypeBll ?? (_dataTypeBll = new ProductBLL()); } }
        
        [HttpPost]
        public JsonResult GetPagedData(DataTableAjaxPostModel model)
        {

            List<ProductModel> dataModel = new List<ProductModel>();

            DataTablePagination<ProductInfo> mPPagination = DataTypeBll.GetListPaged(model.search, model.orderByColumnName
                , model.orderByDirection, model.start, model.length);

            MyConverter<ProductInfo, ProductModel> mGConverter = MyConverterFactory.GetConverter<ProductInfo, ProductModel>();
            dataModel = mGConverter.ConvertToModel(mPPagination.Data);

            return Json(new
            {
                // this is what datatables wants sending back
                draw = model.draw,
                recordsTotal = mPPagination.RecordsTotal,
                recordsFiltered = mPPagination.RecordsFiltered,
                data = dataModel
            });
        }





    }
}

