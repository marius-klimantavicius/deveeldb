//  
//  SystemRegexLibrary.cs
//  
//  Author:
//       Antonello Provenzano <antonello@deveel.com>
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
using System.Text.RegularExpressions;

using Deveel.Data.Collections;

namespace Deveel.Data.Text {
	/// <summary>
	/// The default implementation of the system regular expression library.
	/// </summary>
	class SystemRegexLibrary : IRegexLibrary {
		#region Public Methods
		public bool RegexMatch(string regularExpression, string expressionOps, string value) {
			RegexOptions options = RegexOptions.None;

			if (expressionOps != null) {
				if (expressionOps.IndexOf('i') != -1) {
					options |= RegexOptions.IgnoreCase;
				}
				if (expressionOps.IndexOf('s') != -1) {
					options |= RegexOptions.Singleline;
				}
				if (expressionOps.IndexOf('m') != -1) {
					options |= RegexOptions.Multiline;
				}
			}

			Regex regex = new Regex(regularExpression, options);
			return regex.IsMatch(value);
		}

		public IntegerVector RegexSearch(Table table, int column, string regularExpression, string expressionOps) {
			// Get the ordered column,
			IntegerVector row_list = table.SelectAll(column);
			// The result matched rows,
			IntegerVector result_list = new IntegerVector();

			// Make into a new list that matches the pattern,
			Regex regex;

			try {
				RegexOptions options = RegexOptions.None;
				if (expressionOps != null) {
					if (expressionOps.IndexOf('i') != -1) {
						options |= RegexOptions.IgnoreCase;
					}
					if (expressionOps.IndexOf('s') != -1) {
						options |= RegexOptions.Singleline;
					}
					if (expressionOps.IndexOf('m') != -1) {
						options |= RegexOptions.Multiline;
					}
				}

				regex = new Regex(regularExpression, options);
			} catch (Exception) {
				// Incorrect syntax means we always match to an empty list,
				return result_list;
			}

			// For each row in the column, test it against the regular expression.
			int size = row_list.Count;
			for (int i = 0; i < size; ++i) {
				int row_index = row_list[i];
				TObject cell = table.GetCellContents(column, row_index);
				// Only try and match against non-null cells.
				if (!cell.IsNull) {
					Object ob = cell.Object;
					String str = ob.ToString();
					// If the column matches the regular expression then return it,
					if (regex.IsMatch(str)) {
						result_list.AddInt(row_index);
					}
				}
			}

			return result_list;
		}
		#endregion
	}
}