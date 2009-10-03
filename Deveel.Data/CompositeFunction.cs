// 
//  CompositeFunction.cs
//  
//  Author:
//       Antonello Provenzano <antonello@deveel.com>
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

namespace Deveel.Data {
	/// <summary>
	/// The kind of composite function in a <see cref="CompositeTable"/>.
	/// </summary>
	public enum CompositeFunction {
		/// <summary>
		/// The composite function for finding the union of the tables.
		/// </summary>
		Union = 1,

		/// <summary>
		/// The composite function for finding the interestion of the tables.
		/// </summary>
		Intersect = 2,

		/// <summary>
		/// The composite function for finding the difference of the tables.
		/// </summary>
		Except = 3,

		/// <summary>
		/// An unspecified composite function.
		/// </summary>
		None = -1
	}
}