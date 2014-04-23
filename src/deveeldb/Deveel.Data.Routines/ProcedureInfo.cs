﻿// 
//  Copyright 2010-2014 Deveel
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
using System.Collections.Generic;
using System.Linq;

using Deveel.Data.DbSystem;

namespace Deveel.Data.Routines {
	public sealed class ProcedureInfo : RoutineInfo {
		public ProcedureInfo(RoutineName name) 
			: base(name) {
		}

		public ProcedureInfo(RoutineName name, RoutineParameter[] parameters) 
			: base(name, parameters) {
		}

		internal override bool MatchesInvoke(RoutineInvoke invoke, IQueryContext queryContext) {
			if (invoke == null)
				return false;

			var inputParams = Parameters.Where(parameter => parameter.IsInput).ToList();
			if (invoke.Arguments.Length != inputParams.Count)
				return false;

			for (int i = 0; i < invoke.Arguments.Length; i++) {
				// TODO: support variable evaluation here? or evaluate parameters before reaching here?
				if (!invoke.Arguments[i].IsConstant)
					return false;

				var obj = invoke.Arguments[i].Evaluate(null, null, queryContext);
				var argType = obj.TType;
				var paramType = Parameters[i].Type;

				// TODO: verify if this is assignable (castable) ...
				if (!paramType.IsComparableType(argType))
					return false;
			}

			return true;
		}
	}
}