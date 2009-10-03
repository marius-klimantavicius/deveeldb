// 
//  TableBackedCache.cs
//  
//  Author:
//       Antonello Provenzano <antonello@deveel.com>
//       Tobias Downer <toby@mckoi.com>
//  
//  Copyright (c) 2009 Deveel
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;

using Deveel.Data.Collections;

namespace Deveel.Data {
	/// <summary>
	/// Special type of a cache in a <see cref="TableDataConglomerate"/> that 
	/// is backed by a table in the database.
	/// </summary>
	/// <remarks>
	/// The purpose of this object is to provide efficient access to some 
	/// specific information in a table via a cache.
	/// <para>
	/// This object can be used, for instance, to provide cached access to the system
	/// privilege tables. The engine often performs identical types of priv
	/// queries on the database and it's desirable to cache the access to this
	/// table.
	/// </para>
	/// <para>
	/// This class provides the following services:
	/// <list type="number">
	/// <item>Allows for an instance of this object to be attached to a single
	/// <see cref="DatabaseConnection"/></item>
	/// <item>Listens for any changes that are committed to the table(s) and flushes the
	/// cache as neccessary.</item>
	/// </list>
	/// </para>
	/// <para>
	/// This object is designed to fit into the pure serializable transaction isolation 
	/// system that the system employs. This object will provide a view of the table as 
	/// it was when the transaction started. When the transaction commits (or rollsback) 
	/// the view is updated to the most current version. If a change is committed to the 
	/// tables this cache is backed by, the cache is only flushed when there are no open 
	/// transactions on the
	/// session.
	/// </para>
	/// </remarks>
	abstract class TableBackedCache {
		/// <summary>
		/// The table that this cache is backed by.
		/// </summary>
		private readonly TableName backed_by_table;

		/// <summary>
		/// The list of added rows to the table above when a change 
		/// is committed.
		/// </summary>
		private readonly IntegerVector added_list;

		/// <summary>
		/// The list of removed rows from the table above when a change 
		/// is committed.
		/// </summary>
		private readonly IntegerVector removed_list;

		/// <summary>
		/// Set to true when the backing DatabaseConnection has a transaction open.
		/// </summary>
		private bool transaction_active;

		/// <summary>
		/// The listener object.
		/// </summary>
		private TransactionModificationListener listener;

		protected TableBackedCache(TableName table) {
			this.backed_by_table = table;

			added_list = new IntegerVector();
			removed_list = new IntegerVector();
		}

		/// <summary>
		/// Adds new row ids to the given list.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="list"></param>
		private static void AddRowsToList(int[] from, IntegerVector list) {
			if (from != null) {
				for (int i = 0; i < from.Length; ++i) {
					list.AddInt(from[i]);
				}
			}
		}

		/// <summary>
		/// Attaches this object to a conglomerate.
		/// </summary>
		/// <remarks>
		/// This applies the appropriate listeners to the tables.
		/// </remarks>
		internal void AttachTo(TableDataConglomerate conglomerate) {
			//    TableDataConglomerate conglomerate = connection.getConglomerate();
			TableName table_name = backed_by_table;
			listener = new TransactionModificationListenerImpl(this);
			conglomerate.AddTransactionModificationListener(table_name, listener);
		}

		private class TransactionModificationListenerImpl : TransactionModificationListener {
			private readonly TableBackedCache tbc;

			public TransactionModificationListenerImpl(TableBackedCache tbc) {
				this.tbc = tbc;
			}

			public void OnTableCommitChange(TableCommitModificationEvent evt) {
				TableName table_name = evt.TableName;
				if (table_name.Equals(tbc.backed_by_table)) {
					lock (tbc.removed_list) {
						AddRowsToList(evt.AddedRows, tbc.added_list);
						AddRowsToList(evt.RemovedRows, tbc.removed_list);
					}
				}
			}
		}

		/// <summary>
		/// Call to detach this object from a TableDataConglomerate.
		/// </summary>
		/// <param name="conglomerate"></param>
		internal void DetatchFrom(TableDataConglomerate conglomerate) {
			//    TableDataConglomerate conglomerate = connection.getConglomerate();
			TableName table_name = backed_by_table;
			conglomerate.RemoveTransactionModificationListener(table_name, listener);
		}

		/// <summary>
		/// Called from <see cref="DatabaseConnection"/> to notify 
		/// this object that a new transaction has been started.
		/// </summary>
		/// <remarks>
		/// When a transaction has started, any committed changes to the 
		/// table must NOT be immediately reflected in this cache. Only when 
		/// the transaction commits is there a possibility of the cache 
		/// information being incorrect.
		/// </remarks>
		internal void OnTransactionStarted() {
			transaction_active = true;
			InternalPurgeCache();
		}

		/// <summary>
		/// Called from <see cref="DatabaseConnection"/> to notify that object 
		/// that a transaction has closed.
		/// </summary>
		/// <remarks>
		/// When a transaction is closed, information in the cache may 
		/// be invalidated. For example, if rows 10 - 50 were delete then any
		/// information in the cache that touches this data must be flushed from the
		/// cache.
		/// </remarks>
		internal void OnTransactionFinished() {
			transaction_active = false;
			InternalPurgeCache();
		}

		/// <summary>
		/// Method which copies the <i>added</i> and <i>removed</i> rows and
		/// calls the <see cref="PurgeCache"/>.
		/// </summary>
		private void InternalPurgeCache() {
			// Make copies of the added_list and removed_list
			IntegerVector add, remove;
			lock (removed_list) {
				add = new IntegerVector(added_list);
				remove = new IntegerVector(removed_list);
				// Clear the added and removed list
				added_list.Clear();
				removed_list.Clear();
			}
			// Make changes to the cache
			PurgeCache(add, remove);
		}

		/// <summary>
		/// This method is called when the transaction starts and finishes and must
		/// purge the cache of all invalidated entries.
		/// </summary>
		/// <param name="added_rows"></param>
		/// <param name="removed_rows"></param>
		/// <remarks>
		/// This method must <b>not</b> make any queries on the database. It must
		/// only, at the most, purge the cache of invalid entries.  A trivial
		/// implementation of this might completely clear the cache of all data if
		/// <paramref name="removed_rows"/>.Count &gt; 0.
		/// </remarks>
		internal abstract void PurgeCache(IntegerVector added_rows, IntegerVector removed_rows);
	}
}