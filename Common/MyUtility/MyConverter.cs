using GenericRepository.Model.Id;
using MyFramework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MyUtility
{
    public class MyConverter<TIn, TOut>
    {

        public List<TOut> ConvertToModel(List<TIn> infos)
        {
            List<TOut> models = new List<TOut>();
            foreach(TIn info in infos)
            {
                models.Add(ConvertToModel(info));
            }

            return models;
        }
        public TOut ConvertToModel(TIn info)
        {
            
            PropertyInfo[] pInfos = MyConverterFactory.GetProperties<TIn>();
            Type outputType = typeof(TOut);
            TOut outputInstance = Activator.CreateInstance<TOut>();

            foreach (PropertyInfo pInfo in pInfos)
            {

                PropertyInfo targetPropertyInfo = outputType.GetProperty(pInfo.Name);
                if (targetPropertyInfo == null)
                    continue;

                //bool isNullable = targetPropertyInfo.ToString().Contains("System.Nullable");

                if (pInfo.PropertyType.BaseType == typeof(LongId))
                {
                    if (pInfo.GetValue(info) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        targetPropertyInfo.SetValue(outputInstance, long.Parse(pInfo.GetValue(info).ToString()));
                    }
                }
                else if (pInfo.PropertyType.IsEnum || MyHelper.IsNullableEnum(pInfo.PropertyType))
                {
                    if (pInfo.GetValue(info) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        targetPropertyInfo.SetValue(outputInstance, pInfo.GetValue(info).ToString());
                    }
                }
                else if (pInfo.PropertyType.IsEnum)
                {
                    if (pInfo.GetValue(info) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        targetPropertyInfo.SetValue(outputInstance, pInfo.GetValue(info).ToString());
                    }
                }
                else if (MyHelper.IsNullableEnum(pInfo.PropertyType))
                {
                    if (pInfo.GetValue(info) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        targetPropertyInfo.SetValue(outputInstance, pInfo.GetValue(info).ToString());
                    }
                }
                else if (pInfo.PropertyType == typeof(DateTime) && targetPropertyInfo.PropertyType == typeof(string))
                {
                    //the ViewModel usually have a string for date
                    DateTime dateTime = (DateTime)pInfo.GetValue(info);

                    string dateString = dateTime.ToString(Config.DATETIME_FORMAT);
                    targetPropertyInfo.SetValue(outputInstance, dateString);

                }

                else if (targetPropertyInfo.PropertyType == pInfo.PropertyType)
                {
                    targetPropertyInfo.SetValue(outputInstance, pInfo.GetValue(info));
                }

                else
                {
                    throw new Exception("Unknown property field " + pInfo.PropertyType.FullName);
                }

            }

            return outputInstance;




        }

        public TOut ConvertToInfo(TIn dbModel)
        {
            Type inputType = typeof(TIn);
            Type outputType = typeof(TOut);
            TOut outputInstance = Activator.CreateInstance<TOut>();

            PropertyInfo[] pInfos = MyConverterFactory.GetProperties<TOut>();

            /*
             * it is hard to know if a long is really long or a Id type
             * if a string is Enum or plain string, so the mapping is not done from dbModel to Info
             * rather the mapping is reading info properties, searching similar property in dbModel and mapping
             * If the property name is not same in dbModel and Info, then automated mapping will not set that property
             */ 
            foreach (PropertyInfo pInfo in pInfos)
            {

                PropertyInfo srcPropertyInfo = inputType.GetProperty(pInfo.Name);
                PropertyInfo targetPropertyInfo = outputType.GetProperty(pInfo.Name);

                if (srcPropertyInfo == null)
                    continue;


                if (pInfo.PropertyType.BaseType == typeof(LongId))
                {
                    if (srcPropertyInfo.GetValue(dbModel) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        object property = Activator.CreateInstance(pInfo.PropertyType, srcPropertyInfo.GetValue(dbModel));
                        
                        targetPropertyInfo.SetValue(outputInstance, property);
                    }
                }
                else if (pInfo.PropertyType.IsEnum)
                {
                    if (srcPropertyInfo.GetValue(dbModel) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        object enumType = Enum.Parse(pInfo.PropertyType, srcPropertyInfo.GetValue(dbModel).ToString());

                        targetPropertyInfo.SetValue(outputInstance, enumType);
                    }
                }
                else if (MyHelper.IsNullableEnum(pInfo.PropertyType))
                {
                    Type underlyingType = Nullable.GetUnderlyingType(pInfo.PropertyType);
                    if (srcPropertyInfo.GetValue(dbModel) == null)
                    {
                        targetPropertyInfo.SetValue(outputInstance, null);
                    }
                    else
                    {
                        object enumType = Enum.Parse(underlyingType, srcPropertyInfo.GetValue(dbModel).ToString());

                        targetPropertyInfo.SetValue(outputInstance, enumType);
                    }
                }

                else if (targetPropertyInfo.PropertyType == pInfo.PropertyType)
                {
                    targetPropertyInfo.SetValue(outputInstance, srcPropertyInfo.GetValue(dbModel));
                }

                else
                {
                    throw new Exception("Unknown property field " + pInfo.PropertyType.FullName);
                }

            }

            return outputInstance;




        }


    }

}
