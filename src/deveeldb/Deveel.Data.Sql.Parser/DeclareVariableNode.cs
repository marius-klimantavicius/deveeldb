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

namespace Deveel.Data.Sql.Parser {
	class DeclareVariableNode : SqlNode, IDeclareNode {
		public string VariableName { get; private set; }

		public DataTypeNode Type { get; private set; }

		public bool IsConstant { get; private set; }

		public bool IsNotNull { get; private set; }

		public IExpressionNode DefaultExpression { get; private set; }

		public IExpressionNode AssignExpression { get; private set; }
	}
}
