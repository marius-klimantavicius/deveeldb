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

using Deveel.Data.Sql.Expressions;

namespace Deveel.Data.Sql.Statements {
	[Serializable]
	public sealed class ContinueStatement : LoopControlStatement {
		public ContinueStatement()
			: this((string)null) {
		}

		public ContinueStatement(string label)
			: this(label, null) {
		}

		public ContinueStatement(SqlExpression whenExpression)
			: this(null, whenExpression) {
		}

		public ContinueStatement(string label, SqlExpression whenExpression)
			: base(LoopControlType.Continue, label, whenExpression) {
		}

		private ContinueStatement(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
	}
}
