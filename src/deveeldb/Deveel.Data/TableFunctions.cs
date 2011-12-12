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

namespace Deveel.Data {
	/// <summary>
	/// A number of functions that are table set functions such as simple select
	/// operations, joins, unions, sub-query operations, etc.
	/// </summary>
	public class TableFunctions {
		/// <summary>
		/// The function for a non-correlated ANY or ALL sub-query operation between 
		/// a left and right branch.
		/// </summary>
		/// <param name="left_table"></param>
		/// <param name="left_vars"></param>
		/// <param name="op"></param>
		/// <param name="right_table"></param>
		/// <remarks>
		/// This function only works non-correlated sub-queries.
		/// <para>
		/// A non-correlated sub-query, or a correlated sub-query where the correlated
		/// variables are references to a parent plan branch, the plan only need be
		/// evaluated once and optimizations on the query present themselves.
		/// </para>
		/// </remarks>
		/// <returns></returns>
		/// <example>
		/// An example of an SQL query that generates such a query is:
		/// <code>
		///    Table.col > ANY ( SELECT .... )
		/// </code>
		/// </example>
		internal static Table AnyAllNonCorrelated(Table left_table, VariableName[] left_vars,
										 Operator op, Table right_table) {
			// Check the right table and the correct number of columns,
			if (right_table.ColumnCount != left_vars.Length) {
				throw new Exception("Input table <> " + left_vars.Length + " columns.");
			}

			// Handle trivial case of no entries to select from
			if (left_table.RowCount == 0) {
				return left_table;
			}

			// Resolve the vars in the left table and check the references are
			// compatible.
			int sz = left_vars.Length;
			int[] left_col_map = new int[sz];
			int[] right_col_map = new int[sz];
			for (int i = 0; i < sz; ++i) {
				left_col_map[i] = left_table.FindFieldName(left_vars[i]);
				right_col_map[i] = i;

				//      Console.Out.WriteLine("Finding: " + left_vars[i]);
				//      Console.Out.WriteLine("left_col_map: " + left_col_map[i]);
				//      Console.Out.WriteLine("right_col_map: " + right_col_map[i]);

				if (left_col_map[i] == -1) {
					throw new Exception("Invalid reference: " + left_vars[i]);
				}
				DataTableColumnInfo left_type =
										   left_table.GetColumnDef(left_col_map[i]);
				DataTableColumnInfo right_type = right_table.GetColumnDef(i);
				if (!left_type.TType.IsComparableType(right_type.TType)) {
					throw new ApplicationException(
						"The type of the sub-query expression " + left_vars[i] + "(" +
						left_type.TType.ToSQLString() + ") is incompatible with " +
						"the sub-query type " + right_type.TType.ToSQLString() + ".");
				}
			}

			// We now have all the information to solve this query.

			IList<int> select_vec;

			if (op.IsSubQueryForm(OperatorSubType.All)) {
				// ----- ALL operation -----
				// We work out as follows:
				//   For >, >= type ALL we find the highest value in 'table' and
				//   select from 'source' all the rows that are >, >= than the
				//   highest value.
				//   For <, <= type ALL we find the lowest value in 'table' and
				//   select from 'source' all the rows that are <, <= than the
				//   lowest value.
				//   For = type ALL we see if 'table' contains a single value.  If it
				//   does we select all from 'source' that equals the value, otherwise an
				//   empty table.
				//   For <> type ALL we use the 'not in' algorithm.

				if (op.IsEquivalent(">") || op.IsEquivalent(">=")) {
					// Select the last from the set (the highest value),
					TObject[] highest_cells =
											right_table.GetLastCellContent(right_col_map);
					// Select from the source table all rows that are > or >= to the
					// highest cell,
					select_vec = left_table.SelectRows(left_col_map, op, highest_cells);
				} else if (op.IsEquivalent("<") || op.IsEquivalent("<=")) {
					// Select the first from the set (the lowest value),
					TObject[] lowest_cells =
										   right_table.GetFirstCellContent(right_col_map);
					// Select from the source table all rows that are < or <= to the
					// lowest cell,
					select_vec = left_table.SelectRows(left_col_map, op, lowest_cells);
				} else if (op.IsEquivalent("=")) {
					// Select the single value from the set (if there is one).
					TObject[] single_cell =
										 right_table.GetSingleCellContent(right_col_map);
					if (single_cell != null) {
						// Select all from source_table all values that = this cell
						select_vec = left_table.SelectRows(left_col_map, op, single_cell);
					} else {
						// No single value so return empty set (no value in LHS will equal
						// a value in RHS).
						return left_table.EmptySelect();
					}
				} else if (op.IsEquivalent("<>")) {
					// Equiv. to NOT IN
					select_vec = INHelper.NotIn(left_table, right_table,
												left_col_map, right_col_map);
				} else {
					throw new Exception("Don't understand operator '" + op + "' in ALL.");
				}
			} else if (op.IsSubQueryForm(OperatorSubType.Any)) {

				// ----- ANY operation -----
				// We work out as follows:
				//   For >, >= type ANY we find the lowest value in 'table' and
				//   select from 'source' all the rows that are >, >= than the
				//   lowest value.
				//   For <, <= type ANY we find the highest value in 'table' and
				//   select from 'source' all the rows that are <, <= than the
				//   highest value.
				//   For = type ANY we use same method from INHelper.
				//   For <> type ANY we iterate through 'source' only including those
				//   rows that a <> query on 'table' returns size() != 0.

				if (op.IsEquivalent(">") || op.IsEquivalent(">=")) {
					// Select the first from the set (the lowest value),
					TObject[] lowest_cells =
										   right_table.GetFirstCellContent(right_col_map);
					// Select from the source table all rows that are > or >= to the
					// lowest cell,
					select_vec = left_table.SelectRows(left_col_map, op, lowest_cells);
				} else if (op.IsEquivalent("<") || op.IsEquivalent("<=")) {
					// Select the last from the set (the highest value),
					TObject[] highest_cells =
											right_table.GetLastCellContent(right_col_map);
					// Select from the source table all rows that are < or <= to the
					// highest cell,
					select_vec = left_table.SelectRows(left_col_map, op, highest_cells);
				} else if (op.IsEquivalent("=")) {
					// Equiv. to IN
					select_vec = INHelper.In(left_table, right_table,
											 left_col_map, right_col_map);
				} else if (op.IsEquivalent("<>")) {
					// Select the value that is the same of the entire column
					TObject[] cells = right_table.GetSingleCellContent(right_col_map);
					if (cells != null) {
						// All values from 'source_table' that are <> than the given cell.
						select_vec = left_table.SelectRows(left_col_map, op, cells);
					} else {
						// No, this means there are different values in the given set so the
						// query evaluates to the entire table.
						return left_table;
					}
				} else {
					throw new Exception("Don't understand operator '" + op + "' in ANY.");
				}
			} else {
				throw new Exception("Unrecognised sub-query operator.");
			}

			// Make into a table to return.
			VirtualTable rtable = new VirtualTable(left_table);
			rtable.Set(left_table, select_vec);

			return rtable;
		}
	}
}