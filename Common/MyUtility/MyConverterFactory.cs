using System.Reflection;
using Zebra.Utilities;

namespace MyUtility
{
    public static class MyConverterFactory
    {
        private static ICacheProvider cacheProvider = new DefaultCacheProvider(3600);

        public static MyConverter<TIn, TOut> GetConverter<TIn, TOut>()
        {
            string key = "converter_InputType_" + typeof(TIn).ToString() + "_OutputType_" + typeof(TOut).ToString();
            object item = cacheProvider.Get(key);
            if(item == null)
            {
                MyConverter<TIn, TOut> mGConverter = new MyConverter<TIn, TOut>();
                cacheProvider.Set(key, mGConverter);
                return mGConverter;
            }
            else
            {
                return item as MyConverter<TIn, TOut>;
            }
        }

        public static PropertyInfo[] GetProperties<TIn>()
        {
            string key = "properties_DataType_" + typeof(TIn).ToString();
            object item = cacheProvider.Get(key);
            if (item == null)
            {
                PropertyInfo[] pInfos = typeof(TIn).GetProperties();
                cacheProvider.Set(key, pInfos);
                return pInfos;
            }
            else
            {
                return item as PropertyInfo[];
            }
        }

    }
}
