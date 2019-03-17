using System;

namespace ManagementPortal.Model
{
	/// <summary>
	/// This attribute, when marked on a method, indicates that the method is public and can be called
	/// by anybody without authentication.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
	public class RequireNoDbMappingAttribute : Attribute
	{
	}
}
