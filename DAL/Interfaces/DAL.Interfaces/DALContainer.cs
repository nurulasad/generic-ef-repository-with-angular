using MyFramework;
using MyFramework.IocContainer;

namespace DatabaseLayer.Interfaces.Core
{
    public class DALContainer
    {

		private static IocContainer instance = null;
		

		public static IocContainer Instance
		{
			get
			{
				if (instance == null)
				{
					createContainer();
				}
				return instance;
			}
		}

		private static object createSync = new object();

		private static void createContainer()
		{
			lock (createSync)
			{

				if (instance == null)
				{
					instance = new IocContainer(Config.DAL_ASSEMBLY_NAME, null, null);
				}
			}
		}

		
	}
}
