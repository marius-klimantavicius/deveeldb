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

namespace Deveel.Data.Sql.Compile {
	public sealed class SqlCompileMessage {
		public SqlCompileMessage(CompileMessageLevel level, string text) 
			: this(level, text, null) {
		}

		public SqlCompileMessage(CompileMessageLevel level, string text, SourceLocation location) {
			if (String.IsNullOrEmpty(text))
				throw new ArgumentNullException("text");

			Level = level;
			Text = text;
			Location = location;
		}

		public CompileMessageLevel Level { get; private set; }

		public string Text { get; private set; }

		public SourceLocation Location { get; private set; }
	}
}