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
using System.Globalization;

using Deveel.Data.Types;

namespace Deveel.Data.Routines {
	[Serializable]
	public class FunctionBuilder : IFunction {
		private bool unboundedSeen;
		private static readonly char[] Alpha = "abcdefghkjlmnopqrstuvwxyz".ToCharArray();

		private RoutineName routineName;
		private readonly List<RoutineParameter> parameters = new List<RoutineParameter>();
		private FunctionType functionType = FunctionType.Static;

		private Func<ExecuteContext, ExecuteResult> fullExecuteFunc;
		private Func<TObject[], TObject> evalExeciteFunc;
		private Func<ExecuteContext, TType> returnTypeFunc = context => FirstArgumentType(context);

		private Func<IGroupResolver, IVariableResolver, TObject, TObject, TObject> aggregateFunc;
		private Func<IGroupResolver, IVariableResolver, TObject, TObject> afterAggregateFunc;

		RoutineName IRoutine.Name {
			get { return routineName; }
		}

		RoutineType IRoutine.Type {
			get { return RoutineType.Function; }
		}

		RoutineParameter[] IRoutine.Parameters {
			get { return parameters.ToArray(); }
		}

		FunctionType IFunction.FunctionType {
			get { return functionType; }
		}

		ExecuteResult IRoutine.Execute(ExecuteContext context) {
			if (aggregateFunc != null) {
				if (context.GroupResolver == null)
					throw new Exception("'" + routineName + "' can only be used as an aggregate function.");

				TObject result = null;
				// All aggregates functions return 'null' if group size is 0
				int size = context.GroupResolver.Count;
				if (size == 0) {
					// Return a NULL of the return type
					return context.FunctionResult(new TObject(((IFunction) this).ReturnTType(context), null));
				}

				TObject val;
				VariableName v = context.Arguments[0].AsVariableName();
				// If the aggregate parameter is a simple variable, then use optimal
				// routine,
				if (v != null) {
					for (int i = 0; i < size; ++i) {
						val = context.GroupResolver.Resolve(v, i);
						result = aggregateFunc(context.GroupResolver, context.VariableResolver, result, val);
					}
				} else {
					// Otherwise we must resolve the expression for each entry in group,
					// This allows for expressions such as 'sum(quantity * price)' to
					// work for a group.
					Expression exp = context.Arguments[0];
					for (int i = 0; i < size; ++i) {
						val = exp.Evaluate(null, context.GroupResolver.GetVariableResolver(i), context.QueryContext);
						result = aggregateFunc(context.GroupResolver, context.VariableResolver, result, val);
					}
				}

				// Post method.
				if (afterAggregateFunc != null)
					result = afterAggregateFunc(context.GroupResolver, context.VariableResolver, result);

				return context.FunctionResult(result);
			}

			if (fullExecuteFunc != null)
				return fullExecuteFunc(context);

			if (evalExeciteFunc != null) {
				var returnValue = evalExeciteFunc(context.EvaluatedArguments);
				return context.FunctionResult(returnValue);
			}

			return context.FunctionResult(TObject.Null);
		}

		TType IFunction.ReturnTType(ExecuteContext context) {
			if (returnTypeFunc != null)
				return returnTypeFunc(context);

			return PrimitiveTypes.Numeric;
		}

		private static TType FirstArgumentType(ExecuteContext context) {
			return context.Arguments[0].ReturnTType(context.VariableResolver, context.QueryContext);
		}

		public FunctionBuilder Named(string schema, string name) {
			return Named(RoutineName.Qualify(schema, name));
		}

		public FunctionBuilder Named(RoutineName name) {
			routineName = name;
			return this;
		}

		public FunctionBuilder OfType(FunctionType type) {
			functionType = type;
			return this;
		}

		public FunctionBuilder Static() {
			return OfType(FunctionType.Static);
		}

		public FunctionBuilder Aggregate() {
			return OfType(FunctionType.Aggregate);
		}

		public FunctionBuilder WithParameter(RoutineParameter parameter) {
			if (parameter.IsUnbounded && unboundedSeen)
				throw new InvalidOperationException();

			unboundedSeen = parameter.IsUnbounded;
			parameters.Add(parameter);
			return this;
		}

		public FunctionBuilder WithParameter(string name, TType type) {
			return WithParameter(name, type, ParameterAttributes.None);
		}

		public FunctionBuilder WithParameter(string name, TType type, ParameterAttributes attributes) {
			return WithParameter(name, type, ParameterDirection.Input, attributes);
		}

		public FunctionBuilder WithParameter(string name, TType type, ParameterDirection direction) {
			return WithParameter(name, type, direction, ParameterAttributes.None);
		}

		public FunctionBuilder WithParameter(TType type, ParameterAttributes attributes) {
			return WithParameter(type, ParameterDirection.Input, attributes);
		}

		public FunctionBuilder WithParameter(TType type, ParameterDirection direction) {
			return WithParameter(type, direction, ParameterAttributes.None);
		}

		public FunctionBuilder WithParameter(TType type, ParameterDirection direction, ParameterAttributes attributes) {
			var name = Alpha[parameters.Count].ToString(CultureInfo.InvariantCulture);
			return WithParameter(name, type, direction, attributes);
		}

		public FunctionBuilder WithParameter(string name, TType type, ParameterDirection direction, ParameterAttributes attributes) {
			parameters.Add(new RoutineParameter(name, type, direction, attributes));
			return this;
		}

		public FunctionBuilder WithUnboundedParameter(TType type) {
			var name = Alpha[parameters.Count].ToString(CultureInfo.InvariantCulture);
			return WithUnboundedParameter(name, type);
		}

		public FunctionBuilder WithUnboundedParameter(string name, TType type) {
			if (unboundedSeen)
				throw new InvalidOperationException("An unbounded parameter was already set for this function.");

			unboundedSeen = true;
			return WithParameter(name, type, ParameterDirection.Input, ParameterAttributes.Unbounded);
		}

		public FunctionBuilder WithDynamicParameter(string name) {
			return WithDynamicParameter(name, ParameterAttributes.None);
		}

		public FunctionBuilder WithDynamicParameter(ParameterAttributes attributes) {
			var name = Alpha[parameters.Count].ToString(CultureInfo.InvariantCulture);
			return WithDynamicParameter(name, attributes);
		}

		public FunctionBuilder WithDynamicParameter(string name, ParameterAttributes attributes) {
			return WithParameter(name, Function.DynamicType, attributes);
		}

		public FunctionBuilder WithParameters(RoutineParameter[] funcParameters) {
			if (funcParameters != null) {
				for (int i = 0; i < funcParameters.Length; i++) {
					WithParameter(funcParameters[i]);
				}
			}

			return this;
		}

		public FunctionBuilder OnExecute(Func<ExecuteContext, ExecuteResult> executeFunc) {
			fullExecuteFunc = executeFunc;
			return this;
		}

		public FunctionBuilder OnExecute(Func<TObject[], TObject> executeFunc) {
			evalExeciteFunc = executeFunc;
			return this;
		}

		public FunctionBuilder ReturnsType(TType type) {
			return OnReturnType(context => type);
		}

		public FunctionBuilder OnReturnType(Func<ExecuteContext, TType> func) {
			returnTypeFunc = func;
			return this;
		}

		public FunctionBuilder OnAggregate(Func<IGroupResolver, IVariableResolver, TObject, TObject, TObject> func) {
			aggregateFunc = func;
			return this;
		}

		public FunctionBuilder OnAfterAggregate(Func<IGroupResolver, IVariableResolver, TObject, TObject> func) {
			afterAggregateFunc = func;
			return this;
		}

		public static FunctionBuilder New(string schema, string name) {
			return new FunctionBuilder().Named(schema, name);
		}

		public static FunctionBuilder New(RoutineName name) {
			return new FunctionBuilder().Named(name);
		}
	}
}