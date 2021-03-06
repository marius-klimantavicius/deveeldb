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
using System.Runtime.Serialization;

using Deveel.Data.Security;

namespace Deveel.Data.Sql.Statements {
	[Serializable]
	public sealed class DropViewStatement : SqlStatement {
		public DropViewStatement(ObjectName viewName) 
			: this(viewName, false) {
		}

		public DropViewStatement(ObjectName viewName, bool ifExists) {
			if (viewName == null)
				throw new ArgumentNullException("viewName");

			ViewName = viewName;
			IfExists = ifExists;
		}

		private DropViewStatement(SerializationInfo info, StreamingContext context)
			: base(info, context) {
			ViewName = (ObjectName) info.GetValue("ViewName", typeof (ObjectName));
			IfExists = info.GetBoolean("IfExists");
		}

		public ObjectName ViewName { get; private set; }

		public bool IfExists { get; set; }

		protected override void GetData(SerializationInfo info) {
			info.AddValue("ViewName", ViewName);
			info.AddValue("IfExists", IfExists);
		}

		protected override SqlStatement PrepareStatement(IRequest context) {
			var viewName = context.Access().ResolveObjectName(DbObjectType.View, ViewName);

			if (!IfExists &&
				!context.Access().ViewExists(viewName))
				throw new ObjectNotFoundException(ViewName);

			return new DropViewStatement(viewName, IfExists);
		}

		protected override void ConfigureSecurity(ExecutionContext context) {
			context.Assertions.AddDrop(ViewName, DbObjectType.View);
		}

		protected override void ExecuteStatement(ExecutionContext context) {
			//if (!context.User.CanDrop(DbObjectType.View, ViewName))
			//	throw new MissingPrivilegesException(context.Request.UserName(), ViewName, Privileges.Drop);

			// If the 'only if exists' flag is false, we need to check tables to drop
			// exist first.
			if (!IfExists) {
				// If view doesn't exist, throw an error
				if (!context.Request.Access().ViewExists(ViewName)) {
					throw new ObjectNotFoundException(ViewName,
						String.Format("The view '{0}' does not exist and cannot be dropped.", ViewName));
				}
			}

			// Does the table already exist?
			if (context.Request.Access().ViewExists(ViewName)) {
				// Drop table in the transaction
				context.Request.Access().DropObject(DbObjectType.View, ViewName);

				// Revoke all the grants on the table
				context.Request.Access().RevokeAllGrantsOnView(ViewName);
			}
		}

		protected override void AppendTo(SqlStringBuilder builder) {
			string ifExists = IfExists ? "IF EXISTS " : "";
			builder.AppendFormat("DROP VIEW {0}{1}", ifExists, ViewName);
		}
	}
}
