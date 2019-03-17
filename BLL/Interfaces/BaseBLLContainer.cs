using MyFramework.IocContainer;

namespace BusinessLayer.Interfaces.Core
{
    public abstract class BaseBLLContainer
    {

        private volatile IocContainer instance = null;

        protected IocContainer IocInstance
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

        private object createSync = new object();
        /// <summary>
        /// Purpose of this function is to provide a thread safe method creating the IOC container
        /// </summary>
        private void createContainer()
        {
            if (instance == null)
            {
                lock (createSync)
                {
                    // check if null *again*, because we may have been blocking on another thread
                    // which has just created the container
                    if (instance == null)
                    {
                        instance = CreateContainer();
                    }
                }
            }
        }

        protected abstract IocContainer CreateContainer();
        /// <summary>
        /// Explicity set the container that the DAL will use. Used for substituting a test container.
        /// </summary>
        public void SetContainer(IocContainer container)
        {
            instance = container;
        }
    }
}
