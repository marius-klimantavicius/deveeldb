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

using Deveel.Data.Diagnostics;
using Deveel.Data.Security;
using Deveel.Data.Sql;
using Deveel.Data.Store;
using Deveel.Data.Transactions;

namespace Deveel.Data {
	class SystemUserSession : IUserSession {
		public SystemUserSession(IDatabase database, ITransaction transaction) 
			: this(database, transaction, transaction.CurrentSchema()) {
		}

		public SystemUserSession(IDatabase database, ITransaction transaction, string currentSchema) {
			Database = database;
			CurrentSchema =currentSchema;
			Transaction = transaction;
			SessionInfo = new SessionInfo(User.System, transaction.Isolation);
		}

		public void Dispose() {
			Database = null;
			Transaction = null;
			SessionInfo = null;
		}

		public IDatabase Database { get; private set; }

		public string CurrentSchema { get; private set; }

		public SessionInfo SessionInfo { get; private set; }

		public ITransaction Transaction { get; private set; }

		IEnumerable<KeyValuePair<string, object>> IEventSource.Metadata {
			get { return GetMetaData(); }
		}

		IEventSource IEventSource.ParentSource {
			get { return null; }
		}

		private IEnumerable<KeyValuePair<string, object>> GetMetaData() {
			return new Dictionary<string, object> {
				{EventMetadataKeys.UserName, User.SystemName }
			};
		} 

		public ILargeObject CreateLargeObject(long size, bool compressed) {
			throw new NotSupportedException();
		}

		public ILargeObject GetLargeObject(ObjectId objId) {
			throw new NotSupportedException();
		}

		public void Access(IEnumerable<IDbObject> objects, AccessType accessType) {
			// A system session bypasses any lock
		}

		public void Exit(IEnumerable<IDbObject> objects, AccessType accessType) {
			// A system session does not hold any lock
		}

		public void Lock(IEnumerable<IDbObject> objects, AccessType accessType, LockingMode mode) {
			// A system session does not hold any lock
		}

		public void Commit() {
			Transaction.Commit();
		}

		public void Rollback() {
			Transaction.Rollback();
		}
	}
}