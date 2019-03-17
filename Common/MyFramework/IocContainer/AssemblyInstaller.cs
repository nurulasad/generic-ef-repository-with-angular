using System;
using Castle.Windsor;
using Castle.MicroKernel.Registration;
using System.Reflection;

namespace MyFramework.IocContainer
{
 
    public class AssemblyInstaller : IWindsorInstaller
	{
		private string _assemblyName;
		private Type[] _interceptors;

		public AssemblyInstaller(string assemblyName, params Type[] interceptors)
		{
			this._assemblyName = assemblyName;
            _interceptors = interceptors;
		}

		public void Install(IWindsorContainer container, Castle.MicroKernel.SubSystems.Configuration.IConfigurationStore store)
		{
			// Note: If you get an error here when running a unit test, it is probably because you haven't got the
			// right base class for your test!
			Assembly sourceAssembly = Assembly.Load(_assemblyName);

	
			if (_interceptors.Length > 0)
			{
				foreach (Type interceptor in _interceptors)
				{
					container.Register(Component.For(interceptor));
				}
				
			}

			container.Register(Classes.FromAssembly(sourceAssembly).Pick().If(Component.IsCastleComponent).Configure(performComponentConfiguration));

			
		}

		private void performComponentConfiguration(ComponentRegistration componentReg)
		{
			if (_interceptors.Length > 0)
			{
				componentReg.Interceptors(_interceptors);
			}
		}
	}

}
