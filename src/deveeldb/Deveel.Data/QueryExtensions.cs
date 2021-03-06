﻿// 
//  Copyright 2010-2016 Deveel
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//


using System;
using System.Collections.Generic;
using System.Linq;

using Deveel.Data.Diagnostics;
using Deveel.Data.Mapping;
using Deveel.Data.Routines;
using Deveel.Data.Security;
using Deveel.Data.Sql;
using Deveel.Data.Sql.Compile;
using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Expressions.Build;
using Deveel.Data.Sql.Statements;
using Deveel.Data.Sql.Statements.Build;
using Deveel.Data.Sql.Tables;
using Deveel.Data.Sql.Triggers;
using Deveel.Data.Sql.Types;

namespace Deveel.Data {
	public static class QueryExtensions {
		public static bool IgnoreIdentifiersCase(this IQuery query) {
			return query.Context.IgnoreIdentifiersCase();
		}

		public static void IgnoreIdentifiersCase(this IQuery query, bool value) {
			query.Context.IgnoreIdentifiersCase(value);
		}

		public static void AutoCommit(this IQuery query, bool value) {
			query.Context.AutoCommit(value);
		}

		public static bool AutoCommit(this IQuery query) {
			return query.Context.AutoCommit();
		}

		public static string CurrentSchema(this IQuery query) {
			return query.Context.CurrentSchema();
		}

		public static void CurrentSchema(this IQuery query, string value) {
			query.Context.CurrentSchema(value);
		}

		public static void ParameterStyle(this IQuery query, QueryParameterStyle value) {
			query.Context.ParameterStyle(value);
		}

		public static QueryParameterStyle ParameterStyle(this IQuery query) {
			return query.Context.ParameterStyle();
		}

		#region Execute

		public static StatementResult[] ExecuteQuery(this IQuery query, SqlQuery sqlQuery) {
			if (sqlQuery == null)
				throw new ArgumentNullException("sqlQuery");

			SqlStatement[] statements = null;
			bool cacheGet = false;

			var cache = query.Context.ResolveService<IStatementCache>();
			if (cache != null) {
				if (cache.TryGet(sqlQuery.Text, out statements))
					cacheGet = true;
			}

			if (statements == null) {
				var sqlSouce = sqlQuery.Text;
				var compiler = query.Context.SqlCompiler();

				if (compiler == null)
					compiler = new PlSqlCompiler();

				var compileResult = compiler.Compile(new SqlCompileContext(query.Context, sqlSouce));

				if (compileResult.HasErrors) {
					// TODO: throw a specialized exception...
					throw new InvalidOperationException("The compilation of the query thrown errors.");
				}

				statements = compileResult.Statements.ToArray();
			}

			if (statements.Length == 0)
				return new StatementResult[0];

			if (cache != null && !cacheGet)
				cache.Set(sqlQuery.Text, statements);

			query.Context.RegisterEvent(new QueryEvent(sqlQuery, QueryEventType.BeforeExecute, null));

			var paramStyle = sqlQuery.ParameterStyle;
			if (paramStyle == QueryParameterStyle.Default)
				paramStyle = query.ParameterStyle();

			var preparer = new QueryPreparer(sqlQuery, paramStyle);

			var result = query.ExecuteStatements(preparer, statements);

			query.Context.RegisterEvent(new QueryEvent(sqlQuery, QueryEventType.AfterExecute, result));

			return result;
		}

		public static StatementResult[] ExecuteQuery(this IQuery query, string sqlSource, params QueryParameter[] parameters) {
			var sqlQuery = new SqlQuery(sqlSource);
			if (parameters != null) {
				foreach (var parameter in parameters) {
					sqlQuery.Parameters.Add(parameter);
				}
			}

			return query.ExecuteQuery(sqlQuery);
		}

		public static StatementResult[] ExecuteQuery(this IQuery query, string sqlSource) {
			return query.ExecuteQuery(sqlSource, null);
		}

		private static void Result(StatementResult result) {
			if (result.Type == StatementResultType.Exception)
				throw result.Error;

			if (result.Type != StatementResultType.Result)
				throw new InvalidOperationException("Invalid response from statement");
		}

		#endregion

		#region Statements

		#region Create Schema

		public static void CreateSchema(this IQuery query, string name) {
			query.ExecuteStatement(new CreateSchemaStatement(name));
		}

		#endregion

		#region Drop Schema

		public static void DropSchema(this IQuery query, string name) {
			query.ExecuteStatement(new DropSchemaStatement(name));
		}

		#endregion

		#region Create Table

		public static void CreateTable(this IQuery query, ObjectName tableName, params SqlTableColumn[] columns) {
			CreateTable(query, tableName, false, columns);
		}

		public static void CreateTable(this IQuery query, ObjectName tableName, bool ifNotExists, params SqlTableColumn[] columns) {
			var statement = new CreateTableStatement(tableName, columns);
			statement.IfNotExists = ifNotExists;
			query.ExecuteStatement(statement);
		}

		public static void CreateTemporaryTable(this IQuery query, ObjectName tableName, params SqlTableColumn[] columns) {
			var statement = new CreateTableStatement(tableName, columns);
			statement.Temporary = true;
			Result(query.ExecuteStatement(statement));
		}

		public static void CreateTable(this IQuery query, Type type) {
			var mapInfo = Mapper.GetMapInfo(type);
			query.CreateTable(mapInfo.TableName, mapInfo.Columns.ToArray());

			foreach (var constraint in mapInfo.Constraints) {
				query.AlterTable(mapInfo.TableName, constraint.AsAddConstraintAction());
			}
		}

		public static void CreateTable<T>(this IQuery query) where T : class {
			query.CreateTable(typeof(T));
		}

		public static void CreateTable(this IQuery query, Action<ICreateTableStatementBuilder> createTable) {
			var builder = new CreateTableStatementBuilder();
			createTable(builder);

			var statements = builder.Build();
			foreach (var statement in statements) {
				Result(query.ExecuteStatement(statement));
			}
		}

		#endregion

		#region Alter Table

		public static void AlterTable(this IQuery query, ObjectName tableName, IAlterTableAction action) {
			query.ExecuteStatement(new AlterTableStatement(tableName, action));
		}

		public static void AddColumn(this IQuery query, ObjectName tableName, SqlTableColumn column) {
			query.AlterTable(tableName, new AddColumnAction(column));
		}

		public static void AddColumn(this IQuery query, ObjectName tableName, string columnName, SqlType columnType) {
			query.AddColumn(tableName, new SqlTableColumn(columnName, columnType));
		}

		public static void DropColumn(this IQuery query, ObjectName tableName, string columnName) {
			query.AlterTable(tableName, new DropColumnAction(columnName));
		}

		public static void AddConstraint(this IQuery query, ObjectName tableName, SqlTableConstraint constraint) {
			query.AlterTable(tableName, new AddConstraintAction(constraint));
		}

		public static void AddCheck(this IQuery query, ObjectName tableName, SqlExpression checkExpression) {
			AddCheck(query, tableName, null, checkExpression);
		}

		public static void AddCheck(this IQuery query, ObjectName tableName, string constraintName, SqlExpression checkExpression) {
			query.AddConstraint(tableName, SqlTableConstraint.Check(constraintName, checkExpression));
		}

		public static void AddPrimaryKey(this IQuery query, ObjectName tableName, params string[] columnNames) {
			query.AddPrimaryKey(tableName, null, columnNames);
		}

		public static void AddPrimaryKey(this IQuery query, ObjectName tableName, string constraintName, params string[] columnNames) {
			query.AddConstraint(tableName, SqlTableConstraint.PrimaryKey(constraintName, columnNames));
		}

		public static void AddForeignKey(this IQuery query, ObjectName tableName, string[] columnNames,
			ObjectName foreignTableName, string[] foreignColumns, ForeignKeyAction onDelete, ForeignKeyAction onUpdate) {
			AddForeignKey(query, tableName, null, columnNames, foreignTableName, foreignColumns, onDelete, onUpdate);
		}

		public static void AddForeignKey(this IQuery query, ObjectName tableName, string constraintName, string[] columnNames,
			ObjectName foreignTableName, string[] foreignColumns, ForeignKeyAction onDelete, ForeignKeyAction onUpdate) {
			query.AddConstraint(tableName,
				SqlTableConstraint.ForeignKey(constraintName, columnNames, foreignTableName.FullName, foreignColumns, onDelete,
					onUpdate));
		}

		public static void DropPrimaryKey(this IQuery query, ObjectName tableName) {
			query.AlterTable(tableName, new DropPrimaryKeyAction());
		}

		public static void AddUniqueKey(this IQuery query, ObjectName tableName, params string[] columnNames) {
			AddUniqueKey(query, tableName, null, columnNames);
		}

		public static void AddUniqueKey(this IQuery query, ObjectName tableName, string constraintName, params string[] columnNames) {
			query.AddConstraint(tableName, SqlTableConstraint.UniqueKey(constraintName, columnNames));
		}

		public static void DropConstraint(this IQuery query, ObjectName tableName, string constraintName) {
			query.AlterTable(tableName, new DropConstraintAction(constraintName));
		}

		public static void SetDefault(this IQuery query, ObjectName tableName, string columnName, SqlExpression expression) {
			query.AlterTable(tableName, new SetDefaultAction(columnName, expression));
		}

		public static void DropDefault(this IQuery query, ObjectName tableName, string columnName) {
			query.AlterTable(tableName, new DropDefaultAction(columnName));
		}

		#endregion

		#region Drop Table

		public static void DropTable(this IQuery query, ObjectName tableName) {
			DropTable(query, tableName, false);
		}

		public static void DropTable(this IQuery query, ObjectName tableName, bool ifExists) {
			query.ExecuteStatement(new DropTableStatement(tableName, ifExists));
		}

		#endregion

		#region Create View

		public static void CreateView(this IQuery query, ObjectName viewName, Action<IQueryExpressionBuilder> queryExpression) {
			CreateView(query, viewName, new string[0], queryExpression);
		}

		public static void CreateView(this IQuery query, ObjectName viewName, IEnumerable<string> columnNames, Action<IQueryExpressionBuilder> queryExpression) {
			var builder = new QueryExpressionBuilder();
			queryExpression(builder);

			query.CreateView(viewName, columnNames, builder.Build());
		}

		public static void CreateView(this IQuery query, ObjectName viewName, SqlQueryExpression queryExpression) {
			CreateView(query, viewName, new string[0], queryExpression);
		}

		public static void CreateView(this IQuery query, ObjectName viewName, IEnumerable<string> columnNames,
			SqlQueryExpression queryExpression) {
			Result(query.ExecuteStatement(new CreateViewStatement(viewName, columnNames, queryExpression)));
		}

		#endregion

		#region Drop View

		public static void DropView(this IQuery query, ObjectName viewName) {
			DropView(query, viewName, false);
		}

		public static void DropView(this IQuery query, ObjectName viewName, bool ifExists) {
			query.ExecuteStatement(new DropViewStatement(viewName, ifExists));
		}

		#endregion

		#region Create Group

		public static void CreateRole(this IQuery query, string roleName) {
			query.ExecuteStatement(new CreateRoleStatement(roleName));
		}

		#endregion

		#region Drop Role

		public static void DropRole(this IQuery query, string roleName) {
			query.ExecuteStatement(new DropRoleStatement(roleName));
		}

		#endregion

		#region Create User

		public static void CreateUser(this IQuery query, string userName, SqlExpression password) {
			query.ExecuteStatement(new CreateUserStatement(userName, new Sql.Statements.SqlUserIdentifier(SqlIdentificationType.Password, password)));
		}

		public static void CreateUser(this IQuery query, string userName, string password) {
			query.CreateUser(userName, SqlExpression.Constant(Field.VarChar(password)));
		}

		#endregion

		#region Alter User

		public static void AlterUser(this IQuery query, string userName, IAlterUserAction action) {
			query.ExecuteStatement(new AlterUserStatement(userName, action));
		}

		public static void SetPassword(this IQuery query, string userName, SqlExpression password) {
			query.AlterUser(userName, new SetPasswordAction(password));
		}

		public static void SetPassword(this IQuery query, string userName, string password) {
			query.SetPassword(userName, SqlExpression.Constant(Field.VarChar(password)));
		}

		public static void SetRoles(this IQuery query, string userName, params SqlExpression[] roleNames) {
			query.AlterUser(userName, new SetUserRolesAction(roleNames));
		}

		public static void SetRoles(this IQuery query, string userName, params string[] roleNames) {
			if (roleNames == null)
				throw new ArgumentNullException("roleNames");

			var roles = roleNames.Select(x => SqlExpression.Constant(Field.VarChar(x))).Cast<SqlExpression>().ToArray();
			query.SetRoles(userName, roles);
		}

		public static void SetAccountStatus(this IQuery query, string userName, UserStatus status) {
			query.AlterUser(userName, new SetAccountStatusAction(status));
		}

		public static void SetAccountLocked(this IQuery query, string userName) {
			query.SetAccountStatus(userName, UserStatus.Locked);
		}

		public static void SetAccountUnlocked(this IQuery query, string userName) {
			query.SetAccountStatus(userName, UserStatus.Unlocked);
		}

		#endregion

		#region Drop User

		public static void DropUser(this IQuery query, string userName) {
			query.ExecuteStatement(new DropUserStatement(userName));
		}

		#endregion

		#region Create Type

		public static void CreateType(this IQuery query, ObjectName typeName, params UserTypeMember[] members) {
			CreateType(query, typeName, null, members);
		}

		public static void CreateType(this IQuery query, ObjectName typeName, ObjectName parentType, params UserTypeMember[] members) {
			query.ExecuteStatement(new CreateTypeStatement(typeName, members) {
				ParentTypeName = parentType
			});
		}

		#endregion

		#region DropType

		public static void DropType(this IQuery query, ObjectName typeName) {
			DropType(query, typeName, false);
		}

		public static void DropType(this IQuery query, ObjectName typeName, bool ifExists) {
			query.ExecuteStatement(new DropTypeStatement(typeName, ifExists));
		}

		#endregion


		#region Grant Privileges

		public static void Grant(this IQuery query, string grantee, Privileges privileges, ObjectName objectName) {
			query.Grant(grantee, privileges, false, objectName);
		}

		public static void Grant(this IQuery query, string grantee, Privileges privileges, bool withOption, ObjectName objectName) {
			query.ExecuteStatement(new GrantPrivilegesStatement(grantee, privileges, withOption, objectName));
		}

		#endregion

		#region Grant Role

		public static void GrantRole(this IQuery query, string userName, string roleName) {
			GrantRole(query, userName, roleName, false);
		}

		public static void GrantRole(this IQuery query, string userName, string roleName, bool withAdmin) {
			query.ExecuteStatement(new GrantRoleStatement(userName, roleName, withAdmin));
		}

		#endregion

		#region Revoke Privileges

		public static void Revoke(this IQuery query, string grantee, Privileges privileges, ObjectName objectName) {
			Revoke(query, grantee, privileges, false, objectName);
		}

		public static void Revoke(this IQuery query, string grantee, Privileges privileges, bool grantOption, ObjectName objectName) {
			query.ExecuteStatement(new RevokePrivilegesStatement(grantee, privileges, grantOption, objectName, new string[0]));
		}

		#endregion

		#region Revoke Role

		public static void RevokeRole(this IQuery query, string grantee, string roleName) {
			query.ExecuteStatement(new RevokeRoleStatement(grantee, roleName));
		}

		#endregion

		#region Create Trigger

		public static void CreateTrigger(this IQuery query, ObjectName triggerName, ObjectName tableName, PlSqlBlockStatement body, TriggerEventTime eventTime, TriggerEventType eventType) {
			query.ExecuteStatement(new CreateTriggerStatement(triggerName, tableName, body, eventTime, eventType));
		}


		public static void CreateCallbackTrigger(this IQuery query, string triggerName,  ObjectName tableName, TriggerEventTime eventTime, TriggerEventType eventType) {
			query.ExecuteStatement(new CreateCallbackTriggerStatement(triggerName, tableName, eventTime, eventType));
		}

		public static void CreateProcedureTrigger(this IQuery query, ObjectName triggerName, ObjectName tableName,
			ObjectName procedureName,
			TriggerEventTime eventTime, TriggerEventType eventType) {
			CreateProcedureTrigger(query, triggerName, tableName, procedureName, new InvokeArgument[0], eventTime, eventType);
		}

		public static void CreateProcedureTrigger(this IQuery query, ObjectName triggerName, ObjectName tableName,
			ObjectName procedureName, InvokeArgument[] procedureArgs,
			TriggerEventTime eventTime, TriggerEventType eventType) {
			query.ExecuteStatement(new CreateProcedureTriggerStatement(triggerName, tableName, procedureName, procedureArgs,
				eventTime, eventType));
		}

		public static void CreateProcedureTrigger(this IQuery query, ObjectName triggerName, ObjectName tableName,
	ObjectName procedureName, SqlExpression[] procedureArgs,
	TriggerEventTime eventTime, TriggerEventType eventType) {
			var args = new InvokeArgument[0];
			if (procedureArgs != null)
				args = procedureArgs.Select(x => new InvokeArgument(x)).ToArray();

			query.ExecuteStatement(new CreateProcedureTriggerStatement(triggerName, tableName, procedureName, args,
				eventTime, eventType));
		}

		#endregion

		#region Alter Trigger

		public static void AlterTrigger(this IQuery query, ObjectName triggerName, IAlterTriggerAction action) {
			query.ExecuteStatement(new AlterTriggerStatement(triggerName, action));
		}

		public static void RenameTrigger(this IQuery query, ObjectName triggerName, ObjectName newName) {
			query.AlterTrigger(triggerName, new RenameTriggerAction(newName));
		}

		public static void ChangeTriggerStatus(this IQuery query, ObjectName triggerName, TriggerStatus status) {
			query.AlterTrigger(triggerName, new ChangeTriggerStatusAction(status));
		}

		public static void EnableTrigger(this IQuery query, ObjectName triggerName) {
			query.ChangeTriggerStatus(triggerName, TriggerStatus.Enabled);
		}

		public static void DisableTrigger(this IQuery query, ObjectName triggerName) {
			query.ChangeTriggerStatus(triggerName, TriggerStatus.Disabled);
		}

		#endregion

		#region DropTrigger

		public static void DropTrigger(this IQuery query, ObjectName triggerName) {
			query.ExecuteStatement(new DropTriggerStatement(triggerName));
		}

		public static void DropCallbackTrigger(this IQuery query, string triggerName) {
			query.ExecuteStatement(new DropCallbackTriggersStatement(triggerName));
		}

		#endregion

		#region Create Sequence

		public static void CreateSequence(this IQuery query, ObjectName name, SqlExpression start, SqlExpression increment,
			SqlExpression min, SqlExpression max, SqlExpression cache) {
			CreateSequence(query, name, start, increment, min, max, cache, false);
		}

		public static void CreateSequence(this IQuery query, ObjectName name, SqlExpression start, SqlExpression increment,
			SqlExpression min, SqlExpression max, SqlExpression cache, bool cycle) {
			var statement = new CreateSequenceStatement(name) {
				StartWith = start,
				IncrementBy = increment,
				MinValue = min,
				MaxValue = max,
				Cache = cache,
				Cycle = cycle
			};

			query.ExecuteStatement(statement);
		}

		public static void CreateSequence(this IQuery query, ObjectName name) {
			query.ExecuteStatement(new CreateSequenceStatement(name));
		}

		#endregion

		#region Drop Sequence

		public static void DropSequence(this IQuery query, ObjectName sequenceName) {
			query.ExecuteStatement(new DropSequenceStatement(sequenceName));
		}

		#endregion

		#region Create Function

		public static void CreateFunction(this IQuery query, ObjectName functionName, SqlType returnType, SqlStatement body) {
			CreateFunction(query, functionName, returnType, null, body);
		}

		public static void CreateFunction(this IQuery query, ObjectName functionName, SqlType returnType,
			RoutineParameter[] parameters, SqlStatement body) {
			CreateFunction(query, functionName, returnType, parameters, body, false);
		}

		public static void CreateFunction(this IQuery query, ObjectName functionName, SqlType returnType, SqlStatement body, bool replace) {
			CreateFunction(query, functionName, returnType, null, body, replace);
		}

		public static void CreateFunction(this IQuery query, ObjectName functionName, SqlType returnType,
			RoutineParameter[] parameters, SqlStatement body, bool replace) {
			query.ExecuteStatement(new CreateFunctionStatement(functionName, returnType, parameters, body) {
				ReplaceIfExists = replace
			});
		}

		public static void CreateOrReplaceFunction(this IQuery query, ObjectName functionName, SqlType returnType, SqlStatement body) {
			CreateOrReplaceFunction(query, functionName, returnType, null, body);
		}

		public static void CreateOrReplaceFunction(this IQuery query, ObjectName functionName, SqlType returnType,
	RoutineParameter[] parameters, SqlStatement body) {
			query.CreateFunction(functionName, returnType, parameters, body, true);
		}

		#endregion

		#region Create External Function

		public static void CreateExternFunction(this IQuery query, ObjectName functionName, SqlType returnType,
			RoutineParameter[] parameters, string externRef) {
			CreateExternFunction(query, functionName, returnType, parameters, externRef, false);
		}

		public static void CreateExternFunction(this IQuery query, ObjectName functionName, SqlType returnType, string externRef) {
			CreateExternFunction(query, functionName, returnType, externRef, false);
		}

		public static void CreateExternFunction(this IQuery query, ObjectName functionName, SqlType returnType, string externRef, bool replace) {
			CreateExternFunction(query, functionName, returnType, null, externRef, replace);
		}

		public static void CreateExternFunction(this IQuery query, ObjectName functionName, SqlType returnType,
			RoutineParameter[] parameters, string externRef, bool replace) {
			query.ExecuteStatement(new CreateExternalFunctionStatement(functionName, returnType, parameters, externRef) {
				ReplaceIfExists = replace
			});
		}

		public static void CreateOrReplaceExternFunction(this IQuery query, ObjectName functionName, SqlType returnType, string externRef) {
			CreateOrReplaceExternFunction(query, functionName, returnType, null, externRef);
		}

		public static void CreateOrReplaceExternFunction(this IQuery query, ObjectName functionName, SqlType returnType,
			RoutineParameter[] parameters, string externRef) {
			query.CreateExternFunction(functionName, returnType, parameters, externRef, true);
		}

		#endregion

		#region Drop Function

		public static void DropFunction(this IQuery query, ObjectName functionName) {
			DropFunction(query, functionName, false);
		}

		public static void DropFunction(this IQuery query, ObjectName functionName, bool ifExists) {
			query.ExecuteStatement(new DropFunctionStatement(functionName, ifExists));
		}

		public static void DropFunctionIfExists(this IQuery query, ObjectName functionName) {
			query.DropFunction(functionName, true);
		}

		#endregion

		#region Create Procedure

		public static void CreateProcedure(this IQuery query, ObjectName procedureName, PlSqlBlockStatement body) {
			CreateProcedure(query, procedureName, body, false);
		}

		public static void CreateProcedure(this IQuery query, ObjectName procedureName, PlSqlBlockStatement body, bool replace) {
			CreateProcedure(query, procedureName, null, body, replace);
		}

		public static void CreateProcedure(this IQuery query, ObjectName procedureName,
			RoutineParameter[] parameters, PlSqlBlockStatement body) {
			CreateProcedure(query, procedureName, parameters, body, false);
		}

		public static void CreateProcedure(this IQuery query, ObjectName procedureName,
	RoutineParameter[] parameters, PlSqlBlockStatement body, bool replace) {
			query.ExecuteStatement(new CreateProcedureStatement(procedureName, parameters, body) {
				ReplaceIfExists = replace
			});
		}

		public static void CreateOrReplaceProcedure(this IQuery query, ObjectName procedureName, PlSqlBlockStatement body) {
			CreateOrReplaceProcedure(query, procedureName, null, body);
		}

		public static void CreateOrReplaceProcedure(this IQuery query, ObjectName procedureName,
			RoutineParameter[] parameters, PlSqlBlockStatement body) {
			query.CreateProcedure(procedureName, parameters, body, true);
		}

		public static void CreateExternProcedure(this IQuery query, ObjectName procedureName, string externalRef, bool replace) {
			CreateExternProcedure(query, procedureName, null, externalRef, replace);
		}

		public static void CreateExternProcedure(this IQuery query, ObjectName procedureName, string externalRef) {
			CreateExternProcedure(query, procedureName, null, externalRef);
		}

		public static void CreateExternProcedure(this IQuery query, ObjectName procedureName,
			RoutineParameter[] parameters, string externalRef) {
			CreateExternProcedure(query, procedureName, parameters, externalRef, false);
		}

		public static void CreateExternProcedure(this IQuery query, ObjectName procedureName,
			RoutineParameter[] parameters, string externalRef, bool replace) {
			query.ExecuteStatement(new CreateExternalProcedureStatement(procedureName, parameters, externalRef) {
				ReplaceIfExists = replace
			});
		}

		public static void CreateOrReplaceExternProcedure(this IQuery query, ObjectName procedureName, string externalRef) {
			CreateOrReplaceExternProcedure(query, procedureName, null, externalRef);
		}

		public static void CreateOrReplaceExternProcedure(this IQuery query, ObjectName procedureName,
			RoutineParameter[] parameters, string externalRef) {
			query.CreateExternProcedure(procedureName, parameters, externalRef, true);
		}

		#endregion

		#region Drop Procedure

		public static void DropProcedure(this IQuery query, ObjectName procedureName) {
			DropProcedure(query, procedureName, false);
		}

		public static void DropProcedure(this IQuery query, ObjectName procedureName, bool ifExists) {
			query.ExecuteStatement(new DropProcedureStatement(procedureName, ifExists));
		}

		#endregion

		#region Commit

		public static void Commit(this IQuery query) {
			query.ExecuteStatement(new CommitStatement());
		}

		#endregion

		#region Rollback

		public static void Rollback(this IQuery query) {
			query.ExecuteStatement(new RollbackStatement());
		}

		#endregion

		#endregion

		#region QueryPreparer

		class QueryPreparer : IExpressionPreparer {
			private readonly SqlQuery query;
			private readonly QueryParameterStyle paramStyle;
			private int paramOffset = -1;

			public QueryPreparer(SqlQuery query, QueryParameterStyle paramStyle) {
				this.query = query;
				this.paramStyle = paramStyle;
			}

			public bool CanPrepare(SqlExpression expression) {
				return query.Parameters.Count > 0 &&
				       (expression is SqlVariableReferenceExpression);
			}

			public SqlExpression Prepare(SqlExpression expression) {
				QueryParameter parameter = null;

				var varRef = (SqlVariableReferenceExpression) expression;

				if (varRef.IsParameterReference) {
					if (paramStyle != QueryParameterStyle.Marker)
						return expression;

					parameter = query.Parameters.ElementAt(++paramOffset);
				} else {
					if (paramStyle != QueryParameterStyle.Named)
						return expression;

					var varName = varRef.VariableName;

					parameter = query.Parameters.FindParameter(varName);
				}

				if (parameter == null)
					return expression;

				var value = parameter.SqlType.CreateFrom(parameter.Value);
				var obj = new Field(parameter.SqlType, value);

				return SqlExpression.Constant(obj);
			}
		}

		#endregion

	}
}

