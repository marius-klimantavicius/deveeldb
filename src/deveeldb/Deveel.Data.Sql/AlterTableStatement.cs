// 
//  Copyright 2010  Deveel
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

using System;
using System.Collections;
using System.Collections.Generic;

namespace Deveel.Data.Sql {
	/// <summary>
	/// Logic for the <c>ALTER TABLE</c> SQL statement.
	/// </summary>
	[Serializable]
	public class AlterTableStatement : Statement {
		/// <summary>
		/// The create statement that we use to alter the current table.
		/// </summary>
		/// <remarks>
		/// This is only for compatibility reasons.
		/// </remarks>
		private StatementTree createStatementTree;

		/// <summary>
		/// The name of the table we are altering.
		/// </summary>
		private string tableName;

		/// <summary>
		/// The list of actions to perform in this alter statement.
		/// </summary>
		private IList actions;

		/// <summary>
		/// The TableName object.
		/// </summary>
		private TableName tname;

		/// <summary>
		/// The prepared create table statement.
		/// </summary>
		private CreateTableStatement createStatement;

		public AlterTableStatement(TableName tableName, CreateTableStatement createStatement) {
			TableName = tableName;
			CreateStatement = createStatement;
		}

		public AlterTableStatement(TableName tableName, AlterTableAction action) {
			TableName = tableName;
			Actions.Add(action);
		}

		public AlterTableStatement(TableName tableName, ICollection<AlterTableAction> actions) {
			TableName = tableName;
			foreach(AlterTableAction action in actions)
				Actions.Add(action);
		}

		public AlterTableStatement() {
		}

		/// <summary>
		/// Gets or sets the name (qualified or unqualified) of 
		/// the table to alter.
		/// </summary>
		public TableName TableName {
			get { return TableName.Resolve(GetString("table_name")); }
			set {
				if (value == null)
					throw new ArgumentNullException("value");
				
				SetValue("table_name", value.ToString(false));
			}
		}

		public CreateTableStatement CreateStatement {
			get { return (CreateTableStatement) GetValue("create_statement"); }
			set {
				if (!IsEmpty("alter_action"))
					throw new StatementException("Cannot set a CREATE statement if other actions were set.");
				if (value == null) {
					SetValue("create_statement", null);
				} else {
					SetValue("create_statement", value.Context.StatementTree);
				}
			}
		}

		public IList Actions {
			get { return GetList("alter_actions", true); }
		}

		protected override bool OnListAdd(string key, object value, ref object newValue) {
			if (key == "alter_actions") {
				if (!(value is AlterTableAction))
					throw new ArgumentException("The value must be a " + typeof(AlterTableAction) + ".");

				if (GetValue("create_statement") != null)
					throw new StatementException("Cannot add an action if the CREATE statement was already set.");
			}

			return base.OnListAdd(key, value, ref newValue);
		}

		/// <summary>
		/// Returns true if the column names match.
		/// </summary>
		/// <param name="db"></param>
		/// <param name="col1"></param>
		/// <param name="col2"></param>
		/// <remarks>
		/// If the database is in case insensitive mode then the columns 
		/// will match if the case insensitive search matches.
		/// </remarks>
		/// <returns>
		/// </returns>
		public bool CheckColumnNamesMatch(DatabaseConnection db, String col1, String col2) {
			if (db.IsInCaseInsensitiveMode)
				return String.Compare(col1, col2, true) == 0;
			return col1.Equals(col2);
		}

		private static void CheckColumnConstraint(string columnName, string[] columns, TableName table, string constraintName) {
			foreach (string column in columns) {
				if (columnName.Equals(column)) {
					throw new DatabaseConstraintViolationException(
							DatabaseConstraintViolationException.DropColumnViolation,
							  "Constraint violation (" + constraintName +
							  ") dropping column " + columnName + " because of " +
							  "referential constraint in " + table);
				}
			}

		}



		// ---------- Implemented from Statement ----------

		/// <inheritdoc/>
		protected override void Prepare() {
			// Get variables from the model
			tableName = GetString("table_name");
			actions = GetList("alter_actions", true);
			AlterTableAction action = GetValue("alter_action") as AlterTableAction;
			if (action != null)
				actions.Add(action);

			createStatementTree = (StatementTree)GetValue("create_statement");

			// ---

			if (createStatementTree != null) {
				createStatement = new CreateTableStatement();
				createStatement.Context.Set(Context.Connection, createStatementTree, null);
				tableName = createStatement.tableNameString;
			}

			tname = ResolveTableName(tableName);
			if (tname.Name.IndexOf('.') != -1)
				throw new DatabaseException("Table name can not contain '.' character.");
		}

		/// <inheritdoc/>
		protected override Table Evaluate() {
			DatabaseQueryContext context = new DatabaseQueryContext(Connection);

			// Does the user have privs to alter this tables?
			if (!Connection.Database.CanUserAlterTableObject(context, User, tname)) {
				throw new UserAccessException("User not permitted to alter table: " + tableName);
			}

			if (createStatementTree != null) {
				// Create the data table definition and tell the database to update it.
				DataTableInfo tableInfo = createStatement.CreateTableInfo();
				TableName tname1 = tableInfo.TableName;
				// Is the table in the database already?
				if (Connection.TableExists(tname1)) {
					// Drop any schema for this table,
					Connection.DropAllConstraintsForTable(tname1);
					Connection.UpdateTable(tableInfo);
				}
					// If the table isn't in the database,
				else {
					Connection.CreateTable(tableInfo);
				}

				// Setup the constraints
				createStatement.SetupAllConstraints();

				// Return '0' if we created the table.
				return FunctionTable.ResultTable(context, 0);
			} else {
				// SQL alter command using the alter table actions,

				// Get the table definition for the table name,
				DataTableInfo tableInfo = Connection.GetTable(tname).TableInfo;
				string table_name = tableInfo.Name;
				DataTableInfo newTable = tableInfo.NoColumnClone();

				// Returns a ColumnChecker implementation for this table.
				ColumnChecker checker =
					ColumnChecker.GetStandardColumnChecker(Connection, tname);

				// Set to true if the table topology is alter, or false if only
				// the constraints are changed.
				bool tableAltered = false;

				for (int n = 0; n < tableInfo.ColumnCount; ++n) {
					DataTableColumnInfo column = tableInfo[n].Clone();

					string columnName = column.Name;

					// Apply any actions to this column
					bool markDropped = false;
					foreach (AlterTableAction action in actions) {
						if (action.ActionType == AlterTableActionType.SetDefault &&
						    CheckColumnNamesMatch(Connection, (string) action.Elements[0], columnName)) {
							Expression exp = (Expression) action.Elements[1];
							checker.CheckExpression(exp);
							column.SetDefaultExpression(exp);
							tableAltered = true;
						} else if (action.ActionType == AlterTableActionType.DropDefault &&
						           CheckColumnNamesMatch(Connection, (string) action.Elements[0], columnName)) {
							column.SetDefaultExpression(null);
							tableAltered = true;
						} else if (action.ActionType == AlterTableActionType.DropColumn &&
						           CheckColumnNamesMatch(Connection, (String) action.Elements[0], columnName)) {
							// Check there are no referential links to this column
							DataConstraintInfo[] refs = Connection.QueryTableImportedForeignKeyReferences(tname);
							foreach (DataConstraintInfo reference in refs) {
								CheckColumnConstraint(columnName, reference.ReferencedColumns, reference.ReferencedTableName, reference.Name);
							}
							// Or from it
							refs = Connection.QueryTableForeignKeyReferences(tname);
							foreach (DataConstraintInfo reference in refs) {
								CheckColumnConstraint(columnName, reference.Columns, reference.TableName, reference.Name);
							}

							// Or that it's part of a primary key
							DataConstraintInfo primaryKey = Connection.QueryTablePrimaryKeyGroup(tname);
							if (primaryKey != null)
								CheckColumnConstraint(columnName, primaryKey.Columns, tname, primaryKey.Name);

							// Or that it's part of a unique set
							DataConstraintInfo[] uniques = Connection.QueryTableUniqueGroups(tname);
							foreach (DataConstraintInfo unique in uniques) {
								CheckColumnConstraint(columnName, unique.Columns, tname, unique.Name);
							}

							markDropped = true;
							tableAltered = true;
						}
					}
					// If not dropped then add to the new table definition.
					if (!markDropped) {
						newTable.AddColumn(column);
					}
				}

				// Add any new columns,
				foreach (AlterTableAction action in actions) {
					if (action.ActionType == AlterTableActionType.AddColumn) {
						SqlColumn cdef = (SqlColumn) action.Elements[0];
						if (cdef.IsUnique || cdef.IsPrimaryKey) {
							throw new DatabaseException("Can not use UNIQUE or PRIMARY KEY " +
							                            "column constraint when altering a column.  Use " +
							                            "ADD CONSTRAINT instead.");
						}

						// Convert to a DataTableColumnInfo
						DataTableColumnInfo col = CreateTableStatement.ConvertToColumnInfo(cdef);

						checker.CheckExpression(col.GetDefaultExpression(Connection.System));
						string columnName = col.Name;

						// If column name starts with [table_name]. then strip it off
						col.Name = checker.StripTableName(table_name, columnName);
						newTable.AddColumn(col);
						tableAltered = true;
					}
				}

				// Any constraints to drop...
				foreach (AlterTableAction action in actions) {
					if (action.ActionType == AlterTableActionType.DropConstraint) {
						string constraintName = (string) action.Elements[0];
						int dropCount = Connection.DropNamedConstraint(tname, constraintName);
						if (dropCount == 0)
							throw new DatabaseException("Named constraint to drop on table " + tname + " was not found: " + constraintName);
					} else if (action.ActionType == AlterTableActionType.DropPrimaryKey) {
						if (!Connection.DropPrimaryKeyConstraintForTable(tname, null))
							throw new DatabaseException("No primary key to delete on table " + tname);
					}
				}

				// Any constraints to add...
				foreach (AlterTableAction action in actions) {
					if (action.ActionType == AlterTableActionType.AddConstraint) {
						SqlConstraint constraint = (SqlConstraint) action.Elements[0];
						bool foreignConstraint = (constraint.Type == ConstraintType.ForeignKey);

						TableName refTname = null;
						if (foreignConstraint) {
							refTname = ResolveTableName(constraint.ReferenceTable);
							constraint.ReferenceTable = refTname.ToString();
						}

						checker.StripColumnList(table_name, constraint.ColumnList);
						checker.StripColumnList(constraint.ReferenceTable,
						                        constraint.column_list2);
						checker.CheckExpression(constraint.CheckExpression);
						checker.CheckColumnList(constraint.ColumnList2);
						if (foreignConstraint && constraint.column_list2 != null) {
							ColumnChecker referenced_checker =
								ColumnChecker.GetStandardColumnChecker(Connection, refTname);
							referenced_checker.CheckColumnList(constraint.column_list2);
						}

						CreateTableStatement.AddSchemaConstraint(Connection, tname, constraint);

					}
				}

				// Alter the existing table to the new format...
				if (tableAltered) {
					if (newTable.ColumnCount == 0)
						throw new DatabaseException("Can not ALTER table to have 0 columns.");

					Connection.UpdateTable(newTable);
				} else {
					// If the table wasn't physically altered, check the constraints.
					// Calling this method will also make the transaction check all
					// deferred constraints during the next commit.
					Connection.CheckAllConstraints(tname);
				}

				// Return '0' if everything successful.
				return FunctionTable.ResultTable(context, 0);
			}
		}
	}
}