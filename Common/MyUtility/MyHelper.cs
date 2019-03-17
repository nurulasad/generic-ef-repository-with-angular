using System;

namespace MyUtility
{
    public static class MyHelper
    {

        public static bool IsNullableEnum(Type t)
        {
            Type u = Nullable.GetUnderlyingType(t);
            return (u != null) && u.IsEnum;
        }
    }

}
