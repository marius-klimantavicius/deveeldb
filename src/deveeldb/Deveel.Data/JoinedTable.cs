// 
//  Copyright 2010  Deveel
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
using System.IO;

namespace Deveel.Data {
	/// <summary>
	/// A Table that represents the result of one or more other tables 
	/// joined together.
	/// </summary>
	public abstract class JoinedTable : Table {
		/// <summary>
		/// The list of tables that make up the join.
		/// </summary>
		protected Table[] reference_list;

		/// <summary>
		/// The schemes to describe the entity relation in the given column.
		/// </summary>
		protected SelectableScheme[] column_scheme;

		// These two arrays are lookup tables created in the constructor.  They allow
		// for quick resolution of where a given column should be 'routed' to in
		// the ancestors.

		/// <summary>
		/// Maps the column number in this table to the reference_list array to route to.
		/// </summary>
		protected int[] column_table;

		/// <summary>
		/// Gives a column filter to the given column to route correctly to the ancestor.
		/// </summary>
		protected int[] column_filter;

		/// <summary>
		/// The column that we are sorted against.
		/// </summary>
		/// <remarks>
		/// This is an optimization set by the <see cref="OptimisedPostSet"/> method.
		/// </remarks>
		private int sorted_against_column = -1;

		/// <summary>
		/// The <see cref="TableInfo"/> object that describes the columns and name 
		/// of this table.
		/// </summary>
		private DataTableInfo vtTableInfo;

		/// <summary>
		/// Incremented when the roots are locked.
		/// </summary>
		/// <remarks>
		/// This should only ever be 1 or 0.
		/// </remarks>
		/// <seealso cref="LockRoot"/>
		/// <seealso cref="UnlockRoot"/>
		private byte roots_locked;

		/// <summary>
		/// Constructs the <see cref="JoinedTable"/> with the list of tables in the parent.
		/// </summary>
		/// <param name="tables"></param>
		internal JoinedTable(Table[] tables) {
			CallInit(tables);
		}

		/// <summary>
		/// Constructs the <see cref="JoinedTable"/> with a single table.
		/// </summary>
		/// <param name="table"></param>
		internal JoinedTable(Table table)
			: this(new Table[] { table}) {
		}

		protected JoinedTable() {
		}

		private void CallInit(Table[] tables) {
			Init(tables);
		}

		/// <summary>
		/// Helper function for initializing the variables in the joined table.
		/// </summary>
		/// <param name="tables"></param>
		protected virtual void Init(Table[] tables) {
			int tableCount = tables.Length;
			reference_list = tables;

			int col_count = ColumnCount;
			column_scheme = new SelectableScheme[col_count];

			vtTableInfo = new DataTableInfo(new TableName(null, "#VIRTUAL TABLE#"));

			// Generate look up tables for column_table and column_filter information

			column_table = new int[col_count];
			column_filter = new int[col_count];
			int index = 0;
			for (int i = 0; i < reference_list.Length; ++i) {

				Table curTable = reference_list[i];
				DataTableInfo curTableInfo = curTable.TableInfo;
				int refColCount = curTable.ColumnCount;

				// For each column
				for (int n = 0; n < refColCount; ++n) {
					column_filter[index] = n;
					column_table[index] = i;
					++index;

					// Add this column to the data table info of this table.
					vtTableInfo.AddColumn(curTableInfo[n].Clone());
				}

			}

			vtTableInfo.IsReadOnly = true;
		}

		/// <summary>
		/// Returns a row reference list.
		/// </summary>
		/// <remarks>
		/// <b>Issue</b>: We should be able to optimise these types of things output.
		/// </remarks>
		/// <returns>
		/// Returns an <see cref="IntegerVector"/> that represents a <i>reference</i> 
		/// to the rows in our virtual table.
		/// </returns>
		private IList<int> CalculateRowReferenceList() {
			int size = RowCount;
			List<int> allList = new List<int>(size);
			for (int i = 0; i < size; ++i) {
				allList.Add(i);
			}
			return allList;
		}

		/// <inheritdoc/>
		/// <remarks>
		/// We simply pick the first table to resolve the Database object.
		/// </remarks>
		public override Database Database {
			get { return reference_list[0].Database; }
		}

		/// <inheritdoc/>
		/// <remarks>
		/// This simply returns the column counts in the parent table(s).
		/// </remarks>
		public override int ColumnCount {
			get {
				int column_count_sum = 0;
				for (int i = 0; i < reference_list.Length; ++i) {
					column_count_sum += reference_list[i].ColumnCount;
				}
				return column_count_sum;
			}
		}

		/// <inheritdoc/>
		public override int FindFieldName(VariableName v) {
			int col_index = 0;
			for (int i = 0; i < reference_list.Length; ++i) {
				int col = reference_list[i].FindFieldName(v);
				if (col != -1) {
					return col + col_index;
				}
				col_index += reference_list[i].ColumnCount;
			}
			return -1;
		}

		/// <inheritdoc/>
		public override VariableName GetResolvedVariable(int column) {
			Table parent_table = reference_list[column_table[column]];
			return parent_table.GetResolvedVariable(column_filter[column]);
		}

		/// <summary>
		/// Returns the list of <see cref="Table"/> objects that represent this 
		/// <see cref="VirtualTable"/>.
		/// </summary>
		protected Table[] ReferenceTables {
			get { return reference_list; }
		}

		/// <summary>
		/// This is an optimisation that should only be called <i>after</i> 
		/// a <i>set</i> method has been called.
		/// </summary>
		/// <param name="column"></param>
		/// <remarks>
		/// Because the <c>select</c> operation returns a set that is ordered by the 
		/// given column, we can very easily generate a <see cref="SelectableScheme"/> 
		/// object that can handle this column. So <paramref name="column"/> is the 
		/// column in which this virtual table is naturally ordered by.
		/// <para>
		/// The internals of this method may be totally commented output and the database 
		/// will still operate correctly. However this greatly speeds up situations when 
		/// you perform multiple consequtive operations on the same column.
		/// </para>
		/// </remarks>
		internal void OptimisedPostSet(int column) {
			sorted_against_column = column;
		}

		/// <summary>
		/// Returns a <see cref="SelectableScheme"/> for the given column in the given 
		/// <see cref="VirtualTable"/> row domain.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="originalColumn"></param>
		/// <param name="table"></param>
		/// <remarks>
		/// This searches down through the tables ancestors until it comes across a table 
		/// with a <see cref="SelectableScheme"/> where the given column is fully resolved.
		/// In most cases, this will be the root <see cref="DataTable"/>.
		/// </remarks>
		/// <returns></returns>
		internal override SelectableScheme GetSelectableSchemeFor(int column, int originalColumn, Table table) {

			// First check if the given SelectableScheme is in the column_scheme array
			SelectableScheme scheme = column_scheme[column];
			if (scheme != null) {
				if (table == this) {
					return scheme;
				} else {
					return scheme.GetSubsetScheme(table, originalColumn);
				}
			}

			// If it isn't then we need to calculate it
			SelectableScheme ss;

			// Optimization: The table may be naturally ordered by a column.  If it
			// is we don't try to generate an ordered set.
			if (sorted_against_column != -1 &&
				sorted_against_column == column) {
				InsertSearch isop = new InsertSearch(this, column, CalculateRowReferenceList());
				isop.RecordUid = false;
				ss = isop;
				column_scheme[column] = ss;
				if (table != this) {
					ss = ss.GetSubsetScheme(table, originalColumn);
				}

			} else {
				// Otherwise we must generate the ordered set from the information in
				// a parent index.
				Table parent_table = reference_list[column_table[column]];
				ss = parent_table.GetSelectableSchemeFor(
										 column_filter[column], originalColumn, table);
				if (table == this) {
					column_scheme[column] = ss;
				}
			}

			return ss;
		}

		/// <inheritdoc/>
		internal override void SetToRowTableDomain(int column, IList<int> rowSet, ITableDataSource ancestor) {
			if (ancestor == this)
				return;

			int table_num = column_table[column];
			Table parent_table = reference_list[table_num];

			// Resolve the rows into the parents indices.  (MANGLES row_set)
			ResolveAllRowsForTableAt(rowSet, table_num);

			parent_table.SetToRowTableDomain(column_filter[column], rowSet, ancestor);
		}

		/// <summary>
		/// Returns an object that contains fully resolved, one level only information 
		/// about the <see cref="DataTable"/> and the row indices of the data in this table.
		/// </summary>
		/// <param name="info"></param>
		/// <param name="row_set"></param>
		/// <remarks>
		/// This information can be used to construct a new <see cref="VirtualTable"/>. We 
		/// need to supply an empty <see cref="RawTableInformation"/> object.
		/// </remarks>
		/// <returns></returns>
		private RawTableInformation ResolveToRawTable(RawTableInformation info, IList<int> row_set) {
			if (this is IRootTable) {
				info.Add((IRootTable)this, CalculateRowReferenceList());
			} else {
				for (int i = 0; i < reference_list.Length; ++i) {

					List<int> newRowSet = new List<int>(row_set);

					// Resolve the rows into the parents indices.
					ResolveAllRowsForTableAt(newRowSet, i);

					Table table = reference_list[i];
					if (table is IRootTable) {
						info.Add((IRootTable)table, newRowSet);
					} else {
						((JoinedTable)table).ResolveToRawTable(info, newRowSet);
					}
				}
			}

			return info;
		}

		/// <inheritdoc/>
		internal override RawTableInformation ResolveToRawTable(RawTableInformation info) {
			List<int> all_list = new List<int>();
			int size = RowCount;
			for (int i = 0; i < size; ++i) {
				all_list.Add(i);
			}
			return ResolveToRawTable(info, all_list);
		}

		/// <summary>
		/// Returns the <see cref="TableInfo"/> object that describes the 
		/// columns in this table.
		/// </summary>
		/// <remarks>
		/// For a <see cref="VirtualTable"/>, this object contains the union of all 
		/// the columns in the children in the order set. The name of a virtual table i
		/// s the concat of all the parent table names. The schema is set to null.
		/// </remarks>
		public override DataTableInfo TableInfo {
			get { return vtTableInfo; }
		}

		/// <inheritdoc/>
		public override TObject GetCellContents(int column, int row) {
			int table_num = column_table[column];
			Table parent_table = reference_list[table_num];
			row = ResolveRowForTableAt(row, table_num);
			return parent_table.GetCellContents(column_filter[column], row);
		}

		/// <inheritdoc/>
		public override IRowEnumerator GetRowEnumerator() {
			return new SimpleRowEnumerator(RowCount);
		}

		/// <inheritdoc/>
		internal override void AddDataTableListener(IDataTableListener listener) {
			for (int i = 0; i < reference_list.Length; ++i) {
				reference_list[i].AddDataTableListener(listener);
			}
		}

		/// <inheritdoc/>
		internal override void RemoveDataTableListener(IDataTableListener listener) {
			for (int i = 0; i < reference_list.Length; ++i) {
				reference_list[i].RemoveDataTableListener(listener);
			}
		}


		/// <inheritdoc/>
		public override void LockRoot(int lockKey) {
			// For each table, recurse.
			roots_locked++;
			for (int i = 0; i < reference_list.Length; ++i) {
				reference_list[i].LockRoot(lockKey);
			}
		}

		/// <inheritdoc/>
		public override void UnlockRoot(int lock_key) {
			// For each table, recurse.
			roots_locked--;
			for (int i = 0; i < reference_list.Length; ++i) {
				reference_list[i].UnlockRoot(lock_key);
			}
		}

		/// <inheritdoc/>
		public override bool HasRootsLocked {
			get { return roots_locked != 0; }
		}


		/// <inheritdoc/>
		public override void PrintGraph(TextWriter output, int indent) {
			for (int i = 0; i < indent; ++i) {
				output.Write(' ');
			}
			output.WriteLine("JT[" + GetType());

			for (int i = 0; i < reference_list.Length; ++i) {
				reference_list[i].PrintGraph(output, indent + 2);
			}

			for (int i = 0; i < indent; ++i) {
				output.Write(' ');
			}
			output.WriteLine("]");
		}

		// ---------- Abstract methods ----------

		/// <summary>
		/// Given a row and a table index (to a parent reference table), this will
		/// return the row index in the given parent table for the given row.
		/// </summary>
		/// <param name="row_number"></param>
		/// <param name="table_num"></param>
		/// <returns></returns>
		protected abstract int ResolveRowForTableAt(int row_number, int table_num);

		/// <summary>
		/// Given an <see cref="IntegerVector"/> that represents a list of pointers to rows 
		/// in this table, this resolves the rows to row indexes in the given parent table.
		/// </summary>
		/// <param name="row_set"></param>
		/// <param name="table_num"></param>
		/// <remarks>
		/// This method changes the <paramref name="row_set"/> <see cref="IntegerVector"/> object.
		/// </remarks>
		protected abstract void ResolveAllRowsForTableAt(IList<int> row_set, int table_num);
	}
}