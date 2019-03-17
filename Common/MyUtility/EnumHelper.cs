using System;
using System.Linq;
using System.Reflection;

namespace MyUtility
{
    public static class EnumHelper
    {
        
        
        public static string GetEnumDescription<T>(string value)
        {
            Type type = typeof(T);
            string name = Enum.GetNames(type).Where(f => f.Equals(value, StringComparison.CurrentCultureIgnoreCase)).Select(d => d).FirstOrDefault();

            if (name == null)
            {
                return string.Empty;
            }
            FieldInfo field = type.GetField(name);
            object[] customAttribute = field.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            return customAttribute.Length > 0 ? ((System.ComponentModel.DescriptionAttribute)customAttribute[0]).Description : name;
        }





    }
}
