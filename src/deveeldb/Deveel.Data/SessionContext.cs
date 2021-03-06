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

using Deveel.Data.Diagnostics;
using Deveel.Data.Sql.Triggers;
using Deveel.Data.Transactions;

namespace Deveel.Data {
	/// <summary>
	/// A <see cref="IContext"/> that handle the context of a containing
	/// <see cref="ISession"/> instance.
	/// </summary>
	public sealed class SessionContext : Context, ISessionContext, ITriggerScope {
		private CallbackTriggerManager triggerManager;

		internal SessionContext(ITransactionContext transactionContext)
			: base(transactionContext) {
			EventRegistry = new EventRegistry(this);
			triggerManager = new CallbackTriggerManager(this);
		}

		public ITransactionContext TransactionContext {
			get { return (ITransactionContext)ParentContext; }
		}

		protected override string ContextName {
			get { return ContextNames.Session; }
		}

		/// <summary>
		/// Gets a registry of events fired within the scope of the
		/// session that contains this context.
		/// </summary>
		public EventRegistry EventRegistry { get; private set; }

		IEventRegistry IEventScope.EventRegistry {
			get { return EventRegistry; }
		}

		ITriggerManager ITriggerScope.TriggerManager {
			get { return triggerManager; }
		}
		
		public IQueryContext CreateQueryContext() {
			return new QueryContext(this);
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				if (triggerManager != null)
					triggerManager.Dispose();

				if (EventRegistry != null)
					EventRegistry.Dispose();
			}

			triggerManager = null;
			EventRegistry = null;

			base.Dispose(disposing);
		}
	}
}

