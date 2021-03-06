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

using Deveel.Data.Sql.Tables;
using Deveel.Data.Sql.Types;

namespace Deveel.Data.Security {
	class UsersInit : IDatabaseCreateCallback {
		public void OnDatabaseCreate(IQuery query) {
			CreateTables(query);

			CreateSystemRoles(query);
			CreatePublicUser(query);
			GrantToPublicUser(query);
		}

		private void CreateSystemRoles(IQuery query) {
			query.Access().CreateRole(SystemRoles.SecureAccessRole);
			query.Access().CreateRole(SystemRoles.UserManagerRole);
			query.Access().CreateRole(SystemRoles.SchemaManagerRole);
			query.Access().CreateRole(SystemRoles.LockedRole);
		}

		private void CreatePublicUser(IQuery query) {
			var userName = User.PublicName;
			var userId = new UserIdentification(KnownUserIdentifications.ClearText, "###");
			var userInfo = new UserInfo(userName, userId);

			query.Access().CreateUser(userInfo);
		}

		private void GrantToPublicUser(IQuery context) {
			// TODO: check that a Privilege manager was set first
			context.Access().GrantOnTable(SystemSchema.ProductInfoTableName, User.PublicName, PrivilegeSets.TableRead);
			context.Access().GrantOnTable(SystemSchema.SqlTypesTableName, User.PublicName, PrivilegeSets.TableRead);
			context.Access().GrantOnTable(SystemSchema.PrivilegesTableName, User.PublicName, PrivilegeSets.TableRead);
			context.Access().GrantOnTable(SystemSchema.StatisticsTableName, User.PublicName, PrivilegeSets.TableRead);
			context.Access().GrantOnTable(SystemSchema.VariablesTableName, User.PublicName, PrivilegeSets.TableRead);
			context.Access().GrantOnTable(SystemSchema.SessionInfoTableName, User.PublicName, PrivilegeSets.TableRead);
		}

		private void CreateTables(IQuery context) {
			var tableInfo = new TableInfo(UserManager.UserTableName);
			tableInfo.AddColumn("name", PrimitiveTypes.String());
			// TODO: User table must be completed ...
			tableInfo = tableInfo.AsReadOnly();
			context.Access().CreateSystemTable(tableInfo);

			context.Access().AddPrimaryKey(UserManager.UserTableName, new[] { "name" }, "SYSTEM_USER_PK");

			tableInfo = new TableInfo(UserManager.PasswordTableName);
			tableInfo.AddColumn("user", PrimitiveTypes.String());
			tableInfo.AddColumn("method", PrimitiveTypes.String());
			tableInfo.AddColumn("method_args", PrimitiveTypes.Binary());
			tableInfo.AddColumn("identifier", PrimitiveTypes.String());
			tableInfo = tableInfo.AsReadOnly();
			context.Access().CreateSystemTable(tableInfo);

			tableInfo = new TableInfo(UserManager.UserRoleTableName);
			tableInfo.AddColumn("user", PrimitiveTypes.String());
			tableInfo.AddColumn("role", PrimitiveTypes.String());
			tableInfo.AddColumn("admin", PrimitiveTypes.Boolean());
			tableInfo = tableInfo.AsReadOnly();
			context.Access().CreateSystemTable(tableInfo);

			tableInfo = new TableInfo(UserManager.RoleTableName);
			tableInfo.AddColumn("name", PrimitiveTypes.String(), true);
			tableInfo = tableInfo.AsReadOnly();
			context.Access().CreateSystemTable(tableInfo);

			context.Access().AddPrimaryKey(UserManager.RoleTableName, new[] { "name" }, "SYSTEM_ROLE_PK");

			var fkCol = new[] { "user" };
			var rfkCol = new[] { "role" };
			var refCol = new[] { "name" };
			const ForeignKeyAction onUpdate = ForeignKeyAction.NoAction;
			const ForeignKeyAction onDelete = ForeignKeyAction.Cascade;
			context.Access().AddForeignKey(UserManager.PasswordTableName, fkCol, UserManager.UserTableName, refCol, onDelete,
				onUpdate, "USER_PASSWORD_FK");
			context.Access().AddForeignKey(UserManager.UserRoleTableName, fkCol, UserManager.UserTableName, refCol, onDelete,
				onUpdate, "USER_PRIV_FK");
			context.Access().AddForeignKey(UserManager.UserRoleTableName, rfkCol, UserManager.RoleTableName, refCol, onDelete,
				onUpdate, "USER_ROLE_FK");
		}
	}
}
