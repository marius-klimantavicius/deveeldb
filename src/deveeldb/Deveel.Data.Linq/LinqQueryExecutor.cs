﻿using System;
using System.Collections.Generic;
using System.Linq;

using Deveel.Data.Sql;
using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Statements;

using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Deveel.Data.Linq {
	class LinqQueryExecutor : IQueryExecutor {
		private IQuery query;

		public LinqQueryExecutor(IQuery query) {
			this.query = query;
		}

		private SqlQuery ToQueryExpression(QueryModel queryModel) {
			return SqlQueryGenerator.GenerateSqlQuery(query, queryModel);
		}

		public T ExecuteScalar<T>(QueryModel queryModel) {
			return ExecuteCollection<T>(queryModel).First();
		}

		public T ExecuteSingle<T>(QueryModel queryModel, bool returnDefaultWhenEmpty) {
			return returnDefaultWhenEmpty
				? ExecuteCollection<T>(queryModel).FirstOrDefault()
				: ExecuteCollection<T>(queryModel).First();
		}


		public IEnumerable<T> ExecuteCollection<T>(QueryModel queryModel) {
			var expression = ToQueryExpression(queryModel);
			var results = query.ExecuteQuery(expression);
			if (results.Length == 0)
				return new T[0];

			if (results.Length > 1)
				throw new InvalidOperationException();

			var result = results[0];
			if (result.Type == StatementResultType.Exception)
				throw result.Error;

			if (result.Type == StatementResultType.CursorRef)
				return result.Cursor.Select(x => x.ToObject<T>());

			if (result.Type == StatementResultType.Result)
				return result.Result.Select(x => x.ToObject<T>(query));

			return new T[0];
		}
	}
}