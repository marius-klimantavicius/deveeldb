﻿using System;

using Deveel.Data.Security;
using Deveel.Data.Serialization;
using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Objects;
using Deveel.Data.Sql.Schemas;
using Deveel.Data.Sql.Sequences;

namespace Deveel.Data.Sql.Statements {
	public sealed class CreateSequenceStatement : SqlStatement, IPreparableStatement {
		public CreateSequenceStatement(ObjectName sequenceName) {
			if (sequenceName == null)
				throw new ArgumentNullException("sequenceName");

			SequenceName = sequenceName;
		}

		public ObjectName SequenceName { get; private set; }

		public SqlExpression StartWith { get; set; }

		public SqlExpression IncrementBy { get; set; }

		public SqlExpression MinValue { get; set; }

		public SqlExpression MaxValue { get; set; }

		public SqlExpression Cache { get; set; }

		public bool Cycle { get; set; }

		IStatement IPreparableStatement.Prepare(IRequest request) {
			var schemaName = request.Query.ResolveSchemaName(SequenceName.ParentName);
			var seqName = new ObjectName(schemaName, SequenceName.Name);

			return new Prepared(seqName) {
				StartWith = StartWith,
				IncrementBy = IncrementBy,
				Cache = Cache,
				MinValue = MinValue,
				MaxValue = MaxValue,
				Cycle = Cycle
			};
		}

		#region Prepared

		[Serializable]
		class Prepared : SqlStatement {
			public Prepared(ObjectName sequenceName) {
				SequenceName = sequenceName;
			}

			private Prepared(ObjectData data) {
				SequenceName = data.GetValue<ObjectName>("SequenceName");
			}

			public ObjectName SequenceName { get; private set; }

			public SqlExpression StartWith { get; set; }

			public SqlExpression IncrementBy { get; set; }

			public SqlExpression MinValue { get; set; }

			public SqlExpression MaxValue { get; set; }

			public SqlExpression Cache { get; set; }

			public bool Cycle { get; set; }

			protected override void GetData(SerializeData data) {
				data.SetValue("SequenceName", SequenceName);
			}

			protected override void ExecuteStatement(ExecutionContext context) {
				if (!context.Request.Query.UserCanCreateObject(DbObjectType.Sequence, SequenceName))
					throw new MissingPrivilegesException(context.Request.Query.UserName(), SequenceName, Privileges.Create);

				if (context.Request.Query.ObjectExists(DbObjectType.Sequence, SequenceName))
					throw new InvalidOperationException(String.Format("The sequence '{0}' already exists.", SequenceName));

				var startValue = SqlNumber.Zero;
				var incrementBy = SqlNumber.One;
				var minValue = SqlNumber.Zero;
				var maxValue = new SqlNumber(Int64.MaxValue);
				var cache = 16;
				var cycle = Cycle;
				
				if (StartWith != null)
					startValue = (SqlNumber) StartWith.EvaluateToConstant(context.Request, null).AsBigInt().Value;
				if (IncrementBy != null)
					incrementBy = (SqlNumber) IncrementBy.EvaluateToConstant(context.Request, null).AsBigInt().Value;
				if (MinValue != null)
					minValue = (SqlNumber) MinValue.EvaluateToConstant(context.Request, null).AsBigInt().Value;
				if (MaxValue != null)
					maxValue = (SqlNumber) MaxValue.EvaluateToConstant(context.Request, null).AsBigInt().Value;

				if (minValue >= maxValue)
					throw new InvalidOperationException("The minimum value cannot be more than the maximum.");
				if (startValue < minValue ||
					startValue >= maxValue)
					throw new InvalidOperationException("The start value cannot be out of the mim/max range.");

				var seqInfo = new SequenceInfo(SequenceName, startValue, incrementBy, minValue, maxValue, cache, cycle);
				context.Request.Query.CreateObject(seqInfo);
			}
		}

		#endregion
	}
}