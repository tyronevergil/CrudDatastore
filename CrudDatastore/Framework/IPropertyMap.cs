using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CrudDatastore.Framework
{
    public interface IPropertyMap<T1> where T1 : EntityBase
    {
        IPropertyMap<T1> Map<T2>(Expression<Func<T1, T2>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase;
        IPropertyMap<T1> Map<T2>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase;
        IPropertyMap<T1> Map<T2, T3>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T3, Expression<Func<T1, T2, bool>>>> mapping) where T2 : EntityBase where T3 : EntityBase;
    }
}
