using MyFramework;
using MyFramework.IocContainer;

namespace BusinessLayer.Interfaces.Core
{
    public class CoreBLLContainer : BaseBLLContainer
    {
        public static CoreBLLContainer Singleton = new CoreBLLContainer();
        public static IocContainer Instance
        {
            get
            {
                return Singleton.IocInstance;
            }
        }
        protected override IocContainer CreateContainer()
        {
            return new IocContainer(Config.BLL_ASSEMBLY_NAME,  null, null);
        }
        
	}
}
