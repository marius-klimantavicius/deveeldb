﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Deveel.Data.Design;

using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing;

namespace Deveel.Data.Linq {
	class SqlSelectGeneratorExpressionVisitor : RelinqExpressionVisitor {
		private string lastSource;
		private IDictionary<string, List<string>> items;
		private ExpressionCompileContext compileContext;

		private SqlSelectGeneratorExpressionVisitor(ExpressionCompileContext compileContext) {
			items = new Dictionary<string, List<string>>();
			this.compileContext = compileContext;
		}

		public static string GetSqlExpression(Expression selectExpression, ExpressionCompileContext compileContext) {
			var visitor = new SqlSelectGeneratorExpressionVisitor(compileContext);
			visitor.Visit(selectExpression);
			return visitor.GetSqlExpression();
		}

		private string GetSqlExpression() {
			var list = new List<string>();

			foreach (var itemGroup in items) {
				var tableName = compileContext.FindTableName(itemGroup.Key);

				var members = itemGroup.Value;
				if (members == null || members.Count == 0) {
					list.Add(String.Format("{0}.*", tableName));
				} else {
					list.AddRange( members.Select(x => String.Format("{0}.{1}", tableName, x)));
				}
			}

			return String.Join(", ", list.ToArray());
		}

		protected override Expression VisitMember(MemberExpression expression) {
			var type = expression.Member.ReflectedType;

			var memberInfo = compileContext.GetMemberMap(type, expression.Member.Name);
			if (memberInfo == null)
				throw new NotSupportedException();

			List<string> members;
			if (!items.TryGetValue(lastSource, out members))
				throw new InvalidOperationException();

			members.Add(memberInfo.ColumnName);

			return expression;
		}

		protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression expression) {
			lastSource = expression.ReferencedQuerySource.ItemName;
			items[lastSource] = new List<string>();
			return expression;
		}
	}
}
