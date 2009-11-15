//  
//  IStatementTreeObject.cs
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

namespace Deveel.Data.Sql {
	/// <summary>
	/// A complex object that is to be contained within a <see cref="StatementTree"/> object.
	/// </summary>
	/// <remarks>
	/// A statement tree object must be serializable, and it must be able to
	/// reference all <see cref="Expression"/> objects so that they may be prepared.
	/// </remarks>
	internal interface IStatementTreeObject : ICloneable {
		/// <summary>
		/// Prepares all expressions in this statement tree object by 
		/// passing the <see cref="IExpressionPreparer"/> object to the 
		/// <see cref="Expression.Prepare"/> method of the expression.
		/// </summary>
		/// <param name="preparer"></param>
		void PrepareExpressions(IExpressionPreparer preparer);
	}
}