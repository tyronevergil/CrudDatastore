using System;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore
{
	public class DelegateCrudAdapter<T> : DelegateQueryAdapter<T>, ICrud<T> where T : EntityBase
	{
        private readonly Action<T> _createTrigger;
		private readonly Action<T> _updateTrigger;
		private readonly Action<T> _deleteTrigger;

        public DelegateCrudAdapter(Action<T> createTrigger,
                                    Action<T> updateTrigger,
                                    Action<T> deleteTrigger,
                                    Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(createTrigger, updateTrigger, deleteTrigger, readExpressionTrigger, (sql, parameters) => readExpressionTrigger((e) => false))
        {
        }

        public DelegateCrudAdapter(IDataNavigation dataNavigation,
									Action<T> createTrigger,
                                    Action<T> updateTrigger,
                                    Action<T> deleteTrigger,
                                    Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(dataNavigation, createTrigger, updateTrigger, deleteTrigger, readExpressionTrigger, (sql, parameters) => readExpressionTrigger((e) => false))
        {
        }

        public DelegateCrudAdapter(Action<T> createTrigger,
									Action<T> updateTrigger,
									Action<T> deleteTrigger,
									Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
									Func<string, object[], IQueryable<T>> readCommandTrigger)
			: this(null, createTrigger, updateTrigger, deleteTrigger, readExpressionTrigger, readCommandTrigger)
		{
		}

        public DelegateCrudAdapter(IDataNavigation dataNavigation,
									Action<T> createTrigger,
                                    Action<T> updateTrigger,
                                    Action<T> deleteTrigger,
                                    Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
                                    Func<string, object[], IQueryable<T>> readCommandTrigger)
            : base(dataNavigation, readExpressionTrigger, readCommandTrigger)
        {
            _createTrigger = createTrigger;
            _updateTrigger = updateTrigger;
            _deleteTrigger = deleteTrigger;
        }

        public virtual void Create(T entity)
		{
			_createTrigger(entity);
		}

		public virtual IQuery<T> Read()
		{
			return this;
		}

		public virtual void Update(T entity)
		{
			_updateTrigger(entity);
		}

		public virtual void Delete(T entity)
		{
			_deleteTrigger(entity);
		}
	}
}
