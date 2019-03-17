using GenericRepository.Model.Plain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MyUtility
{

    public static class MyEntitySearchExpression
    {
    
        public static Expression<Func<T, bool>> GetExpression<T>(List<ColumnDefinition> columnDefinitions, string searchValue)
        {
           

            var parameterExp = Expression.Parameter(typeof(T), "category");

            if (string.IsNullOrEmpty(searchValue) )
            {
                return Expression.Lambda<Func<T, bool>>(Expression.Constant(true), parameterExp);
            }

            if ( columnDefinitions == null ||
                (columnDefinitions != null && columnDefinitions.Count == 0))
            {
                return Expression.Lambda<Func<T, bool>>(Expression.Constant(false), parameterExp);
            }

            MethodInfo containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            MethodInfo toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            MethodInfo toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes);

            List<Expression> methodCalls = new List<Expression>();

            foreach (ColumnDefinition columnDefinition in columnDefinitions)
            {
                MemberExpression propertyExp = Expression.Property(parameterExp, columnDefinition.Name);
                ConstantExpression queryValue = Expression.Constant(searchValue.ToLower(), typeof(string));
                
                MethodCallExpression toStringMethodExp = Expression.Call(propertyExp, toStringMethod);

                MethodCallExpression toLowerMethodExp = Expression.Call(toStringMethodExp, toLowerMethod);
                MethodCallExpression containsMethodExp = Expression.Call(toLowerMethodExp, containsMethod, queryValue);
                methodCalls.Add(containsMethodExp);
            }

            Expression orExp = methodCalls.Aggregate((left, right) => Expression.Or(left, right));

            return Expression.Lambda<Func<T, bool>>(orExp, parameterExp);
        }
    }

   
}


