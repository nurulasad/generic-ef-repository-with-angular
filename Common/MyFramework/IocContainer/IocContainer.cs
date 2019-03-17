using System;
using System.Collections.Generic;
using Castle.Windsor;
using Castle.MicroKernel.Registration;
using Castle.Windsor.Installer;
using System.Collections.Concurrent;

namespace MyFramework.IocContainer
{
	
	public class IocContainer : IDisposable
	{
		protected WindsorContainer container = null;

		
		// This will be used by test containers; not to be used by any non-test code.
		protected IocContainer() {}
		
		// Arguably, this should be separate... i am conflating the container and it's configuration here
		public IocContainer(string assemblyName, Type securityCheckInterceptor, Type auditInterceptor)
		{
			List<Type> interceptors = new List<Type>();
			if (securityCheckInterceptor != null)
			{
				interceptors.Add(securityCheckInterceptor);
			}
			if (auditInterceptor != null)
			{
				interceptors.Add(auditInterceptor);
			}

			IWindsorInstaller installer = new AssemblyInstaller(assemblyName, interceptors.ToArray());

            container = new WindsorContainer();
			container.Install(installer);
		}

		public IocContainer(string assemblyName)
			: this (assemblyName, null, null)
		{}

		public virtual T Resolve<T>(ConcurrentDictionary<string, object> argument)
		{
            return container.Resolve<T>(argument);
		}
        public virtual T Resolve<T>()
        {
            return container.Resolve<T>();
        }

        /// <summary>
        /// Any objects with a Transient lifestyle MUST be Release'd after they are Resolve'd
        /// </summary>
        public virtual void Release(object containerObject)
		{
			container.Release(containerObject);
		}

		public void Dispose ()
		{
			if (container != null)
			{
				container.Dispose();
			}
		}
	}
}
