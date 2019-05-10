using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zebra.Web.Client.Test
{
	[TestClass]
	public class AntiForgeryTokenOnAllControllersTestCase
	{
		[TestMethod]
		public void EnsureAntiForgeryToken()
		{
			Assembly webAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.FullName == "Zebra.Web.Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

			// Find all the methods which we expect to be a controller. How do we know something is a controller? It extends System.Web.Mvc.Controller.
			Type[] controllers = webAssembly.GetTypes().Where(t => typeof(System.Web.Mvc.Controller).IsAssignableFrom(t)).ToArray();

			// Given all the controllers, find candidate methods that may in fact be "post" methods.
			// These have....
			// * The attribute returns an ActionResult (of any type)
			// * Don't declare themselves exclusively as HttpGet
			List<MethodInfo> methodsToCheck = new List<MethodInfo>();

			foreach (Type controller in controllers)
			{
				
				// We search only for public methods; which are per instance (i.e. not static) and declared on this class, not inherited.
				foreach (MethodInfo method in controller.GetMethods((BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)))
				{
					// Search for methods that return an ActionResult.
					if (typeof(System.Web.Mvc.ActionResult).IsAssignableFrom(method.ReturnType))
					{
						// If the method doesn't have a "HttpPost" attribute, and does have a "HttpGet" attribute.
						if ((method.CustomAttributes.All(a => a.AttributeType != typeof(System.Web.Mvc.HttpPostAttribute)))&&
							(method.CustomAttributes.Any(a => a.AttributeType == typeof(System.Web.Mvc.HttpGetAttribute))))
						{
							// this is fine. Method has a get attribute and no post attribute.
						}
						else
						{
							// This method needs to be checked.
							methodsToCheck.Add(method);
						}
					}
				}
			}

			// I'm using a hashset here because lazy filtering above means we'll detect the same method multiple times.
			List<string> errors = new List<string>();

			// Now check that all these methods have the [ValidateAntiForgeryToken] attribute.
			foreach (MethodInfo m in methodsToCheck)
			{
				if ((!m.CustomAttributes.Any(a => a.AttributeType == typeof(System.Web.Mvc.ValidateAntiForgeryTokenAttribute))) &&
					(!m.CustomAttributes.Any(a => a.AttributeType == typeof(Zebra.Web.Client.Common.ValidateJsonAntiForgeryTokenAttribute)))&&
					(!m.CustomAttributes.Any(a => a.AttributeType == typeof(Zebra.Web.Client.Common.NoAntiForgeryTokenRequiredAttribute))))
				{
					errors.Add(m.DeclaringType.FullName + "." + m.Name);
				}
			}

			if (errors.Count > 0)
			{
				throw new Exception(
					"The following methods appear to be HTTP Post methods on a controller, but do not implement the ValidateAntiForgeryToken attribute."
					+ Environment.NewLine
					+ string.Join(Environment.NewLine, errors));
			}

		}
	}
}

