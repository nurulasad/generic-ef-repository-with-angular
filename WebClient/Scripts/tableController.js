var app = angular
    .module('tableApp', ['datatables']);

app
    .controller('tableController',
    function ($scope, $http,  DTOptionsBuilder, DTColumnDefBuilder) {
            
            $scope.title = 'Angular data table with server side pagination';

            // this function used to get all data
            var get = function (sSource, aoData, fnCallback, oSettings) {
                var draw = aoData[0].value;
                var order = aoData[2].value;
                var start = aoData[3].value;
                var length = aoData[4].value;
                //var search = aoData[5].value;
                var orderByColumnName = '';
                var orderByDirection = '';
                var search = null;


                if (order.length > 0) {
                    var colIndex = order[0].column;
                    orderByColumnName = aoData[1].value[colIndex].data;
                    orderByDirection = order[0].dir;
                }
                search = aoData[5].value.value;


                var searchInColumns = aoData[1].value.map(function (elem) {
                    return elem.data;
                });
                //.join(",");
                
                model = {
                    'draw': draw,
                    'orderByColumnName': orderByColumnName,
                    'orderByDirection': orderByDirection,
                    'start': start,
                    'length': length,
                    'search': search,
                    'columns': searchInColumns
                };

                $http({
                    url: 'Data/GetPagedData',
                    method: "POST",
                    data: model
                })
                    .then(function (response) {
                        fnCallback(response.data);
                    }); 
            };
        

            $scope.dtOptions = DTOptionsBuilder
                .newOptions()

                .withFnServerData(get) // method name server call
                .withDataProp('data')// parameter name of list use in getLeads Fuction
                //.withOption('rowCallback', rowCallback)
                .withOption('processing', true) //for show progress bar
                .withOption('serverSide', true) // for server side processing
                .withOption('paging', true)// required
                .withPaginationType('full_numbers') // for get full pagination options // first / last / prev / next and page numbers
                .withOption("columns",
                    [
                        { "data": "Id", title:'Id' },
                        { "data": "ProductName", title: 'Name' },
                        { "data": "SupplierId", title: 'SupplierId' },
                        { "data": "UnitPrice", title: 'Price' },
                        { "data": "Package", title: 'Package' },
                        { "data": "IsDiscontinued", title: 'IsDiscontinued' },
                        { "data": "ProductType", title: 'ProductType' },
                        { "data": "Created", title: 'Created' }

                    ])

                .withDisplayLength(10);
                
            
        }
    );