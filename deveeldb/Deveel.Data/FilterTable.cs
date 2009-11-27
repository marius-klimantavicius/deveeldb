//  
//  FilterTable.cs
//  
//  Author:
//       Antonello Provenzano <antonello@deveel.com>
//       Tobias Downer <toby@mckoi.com>
// 
//  Copyright (c) 2009 Deveel
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;

using Deveel.Data.Collections;

namespace Deveel.Data {
	/// <summary>
	/// A table that is a filter for another table.
	/// </summary>
	/// <remarks>
	/// By default, all <see cref="Table"/> methods are implemented to call 
	/// the parent.
	/// This class should be used when we want to implement a table filter of 
	/// some kind. For example, a filter for specific columns, or even rows, etc.
	/// <para>
	/// <b>Note:</b> For efficiency reasons, this will store 
	/// <see cref="SelectableScheme"/> objects generated by the parent like 
	/// <see cref="VirtualTable"/>.
	/// </para>
	/// </remarks>
	public class FilterTable : Table {
		/// <summary>
		/// The schemes to describe the entity relation in the given column.
		/// </summary>
		private SelectableScheme[] column_scheme;

		/// <summary>
		/// The Table we are filtering the columns from.
		/// </summary>
		protected Table parent;

		///<summary>
		///</summary>
		///<param name="parent"></param>
		protected FilterTable(Table parent) {
			this.parent = parent;
		}

		/// <summary>
		/// Returns the parent table.
		/// </summary>
		protected Table Parent {
			get { return parent; }
		}

		public override Database Database {
			get { return parent.Database; }
		}

		public override int ColumnCount {
			get { return parent.ColumnCount; }
		}

		public override int RowCount {
			get { return parent.RowCount; }
		}

		public override DataTableDef DataTableDef {
			get { return parent.DataTableDef; }
		}

		public override bool HasRootsLocked {
			get { return parent.HasRootsLocked; }
		}

		public override int FindFieldName(VariableName v) {
			return parent.FindFieldName(v);
		}

		public override VariableName GetResolvedVariable(int column) {
			return parent.GetResolvedVariable(column);
		}

		internal override SelectableScheme GetSelectableSchemeFor(int column, int original_column, Table table) {
			if (column_scheme == null) {
				column_scheme = new SelectableScheme[parent.ColumnCount];
			}

			// Is there a local scheme available?
			SelectableScheme scheme = column_scheme[column];
			if (scheme == null) {
				// If we are asking for the selectable schema of this table we must
				// tell the parent we are looking for its selectable scheme.
				Table t = table;
				if (table == this) {
					t = parent;
				}

				// Scheme is not cached in this table so ask the parent.
				scheme = parent.GetSelectableSchemeFor(column, original_column, t);
				if (table == this) {
					column_scheme[column] = scheme;
				}
			} else {
				// If this has a cached scheme and we are in the correct domain then
				// return it.
				if (table == this) {
					return scheme;
				} else {
					// Otherwise we must calculate the subset of the scheme
					return scheme.GetSubsetScheme(table, original_column);
				}
			}
			return scheme;
		}

		internal override void SetToRowTableDomain(int column, IntegerVector row_set,
		                                           ITableDataSource ancestor) {
			if (ancestor == this || ancestor == parent) {
				return;
			} else {
				parent.SetToRowTableDomain(column, row_set, ancestor);
			}
		}

		internal override RawTableInformation ResolveToRawTable(RawTableInformation info) {
			return parent.ResolveToRawTable(info);
		}

		public override TObject GetCellContents(int column, int row) {
			return parent.GetCellContents(column, row);
		}

		public override IRowEnumerator GetRowEnumerator() {
			return parent.GetRowEnumerator();
		}

		internal override void AddDataTableListener(IDataTableListener listener) {
			parent.AddDataTableListener(listener);
		}

		internal override void RemoveDataTableListener(IDataTableListener listener) {
			parent.RemoveDataTableListener(listener);
		}

		public override void LockRoot(int lock_key) {
			parent.LockRoot(lock_key);
		}

		public override void UnlockRoot(int lock_key) {
			parent.UnlockRoot(lock_key);
		}


		public override void PrintGraph(TextWriter output, int indent) {
			for (int i = 0; i < indent; ++i) {
				output.Write(' ');
			}
			output.WriteLine("F[" + GetType());

			parent.PrintGraph(output, indent + 2);

			for (int i = 0; i < indent; ++i) {
				output.Write(' ');
			}
			output.WriteLine("]");
		}
	}
}