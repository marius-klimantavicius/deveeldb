﻿// 
//  Copyright 2010-2015 Deveel
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

using Deveel.Data.Sql.Statements;
using Deveel.Data.Sql.Types;

namespace Deveel.Data.Sql.Parser {
	class CreateTypeNode : SqlStatementNode {
		public string TypeName { get; private set; }

		public bool ReplaceIfExists { get; private set; }

		public IEnumerable<TypeAttributeNode> Attributes { get; private set; } 

		protected override void BuildStatement(SqlStatementBuilder builder) {
			var typeName = ObjectName.Parse(TypeName);
			var members = Attributes.Select(x => {
				var type = DataTypeBuilder.Build(builder.TypeResolver, x.Type);
				return new UserTypeMember(x.Name, type);
			});

			builder.AddObject(new CreateTypeStatement(typeName, members) {
				ReplaceIfExists = ReplaceIfExists
			});
		}
	}
}