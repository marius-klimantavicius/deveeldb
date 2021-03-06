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
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Deveel.Data.Sql;

namespace Deveel.Data.Transactions {
	public sealed class LockingQueue {
		private readonly List<Lock> locks;

		internal LockingQueue(IDatabase database, ILockable lockable) {
			Database = database;
			Lockable = lockable;
			locks = new List<Lock>();
		}

		public ILockable Lockable { get; private set; }

		public IDatabase Database { get; private set; }

		public bool IsEmpty {
			get {
				lock (this) {
					return !locks.Any();
				}
			}
		}

		public Lock NewLock(LockingMode mode, AccessType accessType) {
			lock (this) {
				var @lock = new Lock(this, mode, accessType);
				Acquire(@lock);
				@lock.OnAcquired();
				return @lock;
			}
		}

		private void Acquire(Lock @lock) {
			lock (this) {
				Lockable.Acquired(@lock);
				locks.Add(@lock);
			}
		}

		public void Release(Lock @lock) {
			lock (this) {
				locks.Remove(@lock);
				Lockable.Released(@lock);
				Monitor.PulseAll(this);
			}
		}

		private LockTimeoutException TimeoutException(ILockable lockable, AccessType accessType, int timeout) {
			ObjectName tableName;
			if (lockable is IDbObject) {
				tableName = ((IDbObject) lockable).ObjectInfo.FullName;
			} else {
				tableName = new ObjectName(lockable.RefId.ToString());
			}

			return new LockTimeoutException(tableName, accessType, timeout);
		}

		internal void CheckAccess(Lock @lock, int timeout) {
			lock (this) {
				// Error checking.  The queue must contain the Lock.
				if (!locks.Contains(@lock))
					throw new InvalidOperationException("Queue does not contain the given Lock");

				// If 'READ'
				bool blocked;
				int index;
				if (@lock.AccessType == AccessType.Read) {
					do {
						blocked = false;

						index = locks.IndexOf(@lock);

						int i;
						for (i = index - 1; i >= 0 && !blocked; --i) {
							var testLock = locks[i];
							if (testLock.AccessType == AccessType.Write)
								blocked = true;
						}

						if (blocked) {
							if (!Monitor.Wait(this, timeout))
								throw TimeoutException(@lock.Lockable, @lock.AccessType, timeout);
						}
					} while (blocked);
				} else {
					do {
						blocked = false;

						index = locks.IndexOf(@lock);

						if (index != 0) {
							blocked = true;

							if (!Monitor.Wait(this, timeout))
								throw TimeoutException(@lock.Lockable, @lock.AccessType, timeout);
						}

					} while (blocked);
				}

				// Notify the Lock table that we've got a lock on it.
				// TODO: Lock.Table.LockAcquired(Lock);
			}
		}
	}
}