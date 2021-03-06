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

using Deveel.Data.Sql.Types;
using Deveel.Data.Transactions;

namespace Deveel.Data.Sql.Tables {
	class TablesInit : ITableCompositeCreateCallback, ITableCompositeSetupCallback {
		public void OnTableCompositeCreate(IQuery systemQuery) {
			// SYSTEM.PKEY_INFO
			var tableInfo = new TableInfo(SystemSchema.PrimaryKeyInfoTableName);
			tableInfo.AddColumn("id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("name", PrimitiveTypes.String());
			tableInfo.AddColumn("schema", PrimitiveTypes.String());
			tableInfo.AddColumn("table", PrimitiveTypes.String());
			tableInfo.AddColumn("deferred", PrimitiveTypes.Numeric());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);

			// SYSTEM.PKEY_COLS
			tableInfo = new TableInfo(SystemSchema.PrimaryKeyColumnsTableName);
			tableInfo.AddColumn("pk_id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("column", PrimitiveTypes.String());
			tableInfo.AddColumn("seq_no", PrimitiveTypes.Numeric());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);

			// SYSTEM.FKEY_INFO
			tableInfo = new TableInfo(SystemSchema.ForeignKeyInfoTableName);
			tableInfo.AddColumn("id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("name", PrimitiveTypes.String());
			tableInfo.AddColumn("schema", PrimitiveTypes.String());
			tableInfo.AddColumn("table", PrimitiveTypes.String());
			tableInfo.AddColumn("ref_schema", PrimitiveTypes.String());
			tableInfo.AddColumn("ref_table", PrimitiveTypes.String());
			tableInfo.AddColumn("update_rule", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("delete_rule", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("deferred", PrimitiveTypes.Numeric());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);

			// SYSTEM.FKEY_COLS
			tableInfo = new TableInfo(SystemSchema.ForeignKeyColumnsTableName);
			tableInfo.AddColumn("fk_id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("fcolumn", PrimitiveTypes.String());
			tableInfo.AddColumn("pcolumn", PrimitiveTypes.String());
			tableInfo.AddColumn("seq_no", PrimitiveTypes.Numeric());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);

			// SYSTEM.UNIQUE_INFO
			tableInfo = new TableInfo(SystemSchema.UniqueKeyInfoTableName);
			tableInfo.AddColumn("id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("name", PrimitiveTypes.String());
			tableInfo.AddColumn("schema", PrimitiveTypes.String());
			tableInfo.AddColumn("table", PrimitiveTypes.String());
			tableInfo.AddColumn("deferred", PrimitiveTypes.Numeric());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);

			// SYSTEM.UNIQUE_COLS
			tableInfo = new TableInfo(SystemSchema.UniqueKeyColumnsTableName);
			tableInfo.AddColumn("un_id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("column", PrimitiveTypes.String());
			tableInfo.AddColumn("seq_no", PrimitiveTypes.Numeric());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);

			// SYSTEM.CHECK_INFO
			tableInfo = new TableInfo(SystemSchema.CheckInfoTableName);
			tableInfo.AddColumn("id", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("name", PrimitiveTypes.String());
			tableInfo.AddColumn("schema", PrimitiveTypes.String());
			tableInfo.AddColumn("table", PrimitiveTypes.String());
			tableInfo.AddColumn("expression", PrimitiveTypes.String());
			tableInfo.AddColumn("deferred", PrimitiveTypes.Numeric());
			tableInfo.AddColumn("serialized_expression", PrimitiveTypes.Binary());
			tableInfo = tableInfo.AsReadOnly();
			systemQuery.Access().CreateTable(tableInfo);
		}

		private void AddForeignKeys(IQuery systemQuery) {
			// -- Primary Keys --
			// The 'id' columns are primary keys on all the system tables,
			var idCol = new[] { "id" };
			systemQuery.Access().AddPrimaryKey(SystemSchema.PrimaryKeyInfoTableName, idCol, "SYSTEM_PK_PK");
			systemQuery.Access().AddPrimaryKey(SystemSchema.ForeignKeyInfoTableName, idCol, "SYSTEM_FK_PK");
			systemQuery.Access().AddPrimaryKey(SystemSchema.UniqueKeyInfoTableName, idCol, "SYSTEM_UNIQUE_PK");
			systemQuery.Access().AddPrimaryKey(SystemSchema.CheckInfoTableName, idCol, "SYSTEM_CHECK_PK");
			systemQuery.Access().AddPrimaryKey(SystemSchema.SchemaInfoTableName, idCol, "SYSTEM_SCHEMA_PK");

			// -- Foreign Keys --
			// Create the foreign key references,
			var fkCol = new string[1];
			var fkRefCol = new[] { "id" };

			fkCol[0] = "pk_id";
			systemQuery.Access().AddForeignKey(SystemSchema.PrimaryKeyColumnsTableName, fkCol, SystemSchema.PrimaryKeyInfoTableName, fkRefCol, "SYSTEM_PK_FK");

			fkCol[0] = "fk_id";
			systemQuery.Access().AddForeignKey(SystemSchema.ForeignKeyColumnsTableName, fkCol, SystemSchema.ForeignKeyInfoTableName, fkRefCol, "SYSTEM_FK_FK");

			fkCol[0] = "un_id";
			systemQuery.Access().AddForeignKey(SystemSchema.UniqueKeyColumnsTableName, fkCol, SystemSchema.UniqueKeyInfoTableName, fkRefCol, "SYSTEM_UNIQUE_FK");

			// pkey_info 'schema', 'table' column is a unique set,
			// (You are only allowed one primary key per table).
			var columns = new[] { "schema", "table" };
			systemQuery.Access().AddUniqueKey(SystemSchema.PrimaryKeyInfoTableName, columns, "SYSTEM_PKEY_ST_UNIQUE");

			// schema_info 'name' column is a unique column,
			columns = new String[] { "name" };
			systemQuery.Access().AddUniqueKey(SystemSchema.SchemaInfoTableName, columns, "SYSTEM_SCHEMA_UNIQUE");

			//    columns = new String[] { "name" };
			columns = new String[] { "name", "schema" };
			// pkey_info 'name' column is a unique column,
			systemQuery.Access().AddUniqueKey(SystemSchema.PrimaryKeyInfoTableName, columns, "SYSTEM_PKEY_UNIQUE");

			// fkey_info 'name' column is a unique column,
			systemQuery.Access().AddUniqueKey(SystemSchema.ForeignKeyInfoTableName, columns, "SYSTEM_FKEY_UNIQUE");

			// unique_info 'name' column is a unique column,
			systemQuery.Access().AddUniqueKey(SystemSchema.UniqueKeyInfoTableName, columns, "SYSTEM_UNIQUE_UNIQUE");

			// check_info 'name' column is a unique column,
			systemQuery.Access().AddUniqueKey(SystemSchema.CheckInfoTableName, columns, "SYSTEM_CHECK_UNIQUE");
		}

		public void OnTableCompositeSetup(IQuery systemQuery) {
			AddForeignKeys(systemQuery);
		}
	}
}
