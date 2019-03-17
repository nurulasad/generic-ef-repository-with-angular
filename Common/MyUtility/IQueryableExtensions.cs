using System;
using System.Linq;
using System.Linq.Expressions;

namespace MyUtility.Extensions
{
    public static class IQueryableExtensions
    {
        public static IQueryable<TEntity> MyOrderBy<TEntity>(this IQueryable<TEntity> source, string orderByProperty,
                          bool desc)
        {
            string command = desc ? "OrderByDescending" : "OrderBy";
            var type = typeof(TEntity);
            var property = type.GetProperty(orderByProperty);

            //in linq, it is mandatory to have the order by to make a pagination (skip, take)
            //hence we can not ignore the order by clause
            
            if(property == null)
            {
                //if property is not found, this means no sorting requesting. make the asc sorting on first column
                property = type.GetProperties().First();
                command = "OrderBy";
            }

            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExpression = Expression.Lambda(propertyAccess, parameter);
            var resultExpression = Expression.Call(typeof(Queryable), command, new Type[] { type, property.PropertyType },
                                          source.Expression, Expression.Quote(orderByExpression));
            return source.Provider.CreateQuery<TEntity>(resultExpression);
        }
    }
}
