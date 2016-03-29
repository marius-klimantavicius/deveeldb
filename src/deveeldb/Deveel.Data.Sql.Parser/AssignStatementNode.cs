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

using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Statements;

namespace Deveel.Data.Sql.Parser {
	class AssignStatementNode : SqlStatementNode {
		public string VariableName { get; private set; }

		public IExpressionNode Value { get; private set; }

		protected override ISqlNode OnChildNode(ISqlNode node) {
			if (node is IdentifierNode) {
				VariableName = ((IdentifierNode) node).Text;
			} else if (node is IExpressionNode) {
				Value = (IExpressionNode) node;
			}

			return base.OnChildNode(node);
		}

		protected override void BuildStatement(SqlStatementBuilder builder) {
			var variable = VariableName;
			if (String.IsNullOrEmpty(VariableName))
				throw Error("The variable name was not present");

			if (variable[0] == ':')
				variable = variable.Substring(1);

			if (String.IsNullOrEmpty(variable))
				throw Error("The name of the variable is invalid: cannot specify only ':' pointer.");

			var varRef = SqlExpression.VariableReference(variable);
			var value = ExpressionBuilder.Build(Value);

			builder.AddObject(new AssignVariableStatement(varRef, value));
		}
	}
}