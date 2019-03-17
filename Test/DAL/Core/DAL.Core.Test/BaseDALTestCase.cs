using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Transactions;
using System.Data.SqlClient;



namespace DAL.Core.Test
{
    [TestClass]
    public class BaseDALTestCase
    {
      
        
        
        protected static TransactionScope CreateTransaction()
        {
            TransactionOptions options = new TransactionOptions();
            options.IsolationLevel = IsolationLevel.ReadCommitted;

            return new TransactionScope(TransactionScopeOption.Required, options);
        }
        

    }
}
