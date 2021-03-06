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

using Antlr4.Runtime.Misc;

using Deveel.Data.Security;
using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Statements;

namespace Deveel.Data.Sql.Compile {
	static class UserStatements {
		public static SqlStatement Create(PlSqlParser.CreateUserStatementContext context) {
			var userName = context.userName().GetText();
			SqlExpression arg;
			SqlIdentificationType type;
			if (context.byPassword() != null) {
				arg = SqlExpression.Constant(InputString.AsNotQuoted(context.byPassword().CHAR_STRING().GetText()));
				type = SqlIdentificationType.Password;
			} else if (context.externalId() != null) {
				arg = SqlExpression.Constant(InputString.AsNotQuoted(context.externalId().CHAR_STRING()));
				type = SqlIdentificationType.External;
			} else if (context.globalId() != null) {
				arg = SqlExpression.Constant(InputString.AsNotQuoted(context.globalId().CHAR_STRING()));
				type = SqlIdentificationType.Global;
			} else {
				throw new ParseCanceledException();
			}

			var id = new SqlUserIdentifier(type, arg);
			return new CreateUserStatement(userName, id);
		}

		private static SetPasswordAction SetPassword(PlSqlParser.AlterUserIdActionContext context) {
			string password = null;
			if (context.byPassword() != null) {
				password = InputString.AsNotQuoted(context.byPassword().CHAR_STRING().GetText());
			} else if (context.externalId() != null) {
				throw new NotImplementedException();
			} else if (context.globalId() != null) {
				throw new NotImplementedException();
			} else {
				throw new ParseCanceledException();
			}

			return new SetPasswordAction(SqlExpression.Constant(password));
		}

		public static SqlStatement Alter(PlSqlParser.AlterUserStatementContext context) {
			var userName = context.userName().GetText();

			var actions = new List<IAlterUserAction>();

			foreach (var actionContext in context.alterUserAction()) {
				if (actionContext.alterUserIdAction() != null) {
					actions.Add(SetPassword(actionContext.alterUserIdAction()));
				} else if (actionContext.setAccountAction() != null) {
					actions.Add(SetAccount(actionContext.setAccountAction()));
				} else if (actionContext.setRoleAction() != null) {
					actions.Add(SetRole(actionContext.setRoleAction()));
				}
			}

			if (actions.Count == 1)
				return new AlterUserStatement(userName, actions[0]);

			var seq = new SequenceOfStatements();
			foreach (var action in actions) {
				seq.Statements.Add(new AlterUserStatement(userName, action));
			}

			return seq;
		}

		private static IAlterUserAction SetRole(PlSqlParser.SetRoleActionContext context) {
			var roleName = context.regular_id().GetText();

			return new SetUserRolesAction(new[] {SqlExpression.Constant(roleName)});
		}

		private static IAlterUserAction SetAccount(PlSqlParser.SetAccountActionContext context) {
			UserStatus status;
			if (context.LOCK() != null) {
				status = UserStatus.Locked;
			} else if (context.UNLOCK() != null) {
				status = UserStatus.Unlocked;
			} else {
				throw new ParseCanceledException();
			}

			return new SetAccountStatusAction(status);
		}
	}
}
