using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Framework.Internal
{
    internal class EntityCollection<T> : ICollection<T>, IQueryable<T>, IQuery<T>, IEntityCollection where T : EntityBase
    {
        private readonly IQueryable<T> _data;
        private readonly Lazy<IList<T>> _lazyList;

        private readonly Action<T> _markNew;
        private readonly Action<T> _markDeleted;

        public EntityCollection(IQueryable<T> data)
            : this(data, null, null)
        { }

        public EntityCollection(IQueryable<T> data, Action<T> markNew, Action<T> markDeleted)
        {
            _data = data;
            _markNew = markNew;
            _markDeleted = markDeleted;

            _lazyList = new Lazy<IList<T>>(() =>
            {
                return _data.ToList();
            });
        }

        public int Count
        {
            get { return _lazyList.Value.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Add(T item)
        {
            _lazyList.Value.Add(item);

            if (_markNew != null)
                _markNew(item);
        }

        public bool Remove(T item)
        {
            var b = _lazyList.Value.Remove(item);
            if (b)
            {
                if (_markDeleted != null)
                    _markDeleted(item);
            }

            return b;
        }

        public void Clear()
        {
            for (var i = _lazyList.Value.Count - 1; i >= 0; i--)
            {
                Remove(_lazyList.Value[i]);
            }
        }

        public bool Contains(T item)
        {
            return _lazyList.Value.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _lazyList.Value.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _lazyList.Value.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _lazyList.Value.GetEnumerator();
        }

        public IQueryable<T> Execute(Expression<Func<T, bool>> predicate)
        {
            return _lazyList.Value.Where(predicate.Compile()).AsQueryable();
        }

        public Task<IQueryable<T>> ExecuteAsync(Expression<Func<T, bool>> predicate)
        {
            return Task.Run(() => Execute(predicate));
        }

        public IQueryable<T> Execute(string command, params object[] parameters)
        {
            return Execute(e => false);
        }

        public Task<IQueryable<T>> ExecuteAsync(string command, params object[] parameters)
        {
            return ExecuteAsync(e => false);
        }

        public Type ElementType
        {
            get { return _data.ElementType; }
        }

        public Expression Expression
        {
            get { return _data.Expression; }
        }

        public IQueryProvider Provider
        {
            get { return _data.Provider; }
        }

        public virtual void Dispose()
        {
        }
    }
}
