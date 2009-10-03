// 
//  ModFunction.cs
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

namespace Deveel.Data.Functions {
	internal sealed class ModFunction : Function {

		public ModFunction(Expression[] parameters)
			: base("mod", parameters) {

			if (ParameterCount != 2) {
				throw new Exception("Mod function must have two arguments.");
			}
		}

		public override TObject Evaluate(IGroupResolver group, IVariableResolver resolver,
		                                 IQueryContext context) {
			TObject ob1 = this[0].Evaluate(group, resolver, context);
			TObject ob2 = this[1].Evaluate(group, resolver, context);
			if (ob1.IsNull) {
				return ob1;
			} else if (ob2.IsNull) {
				return ob2;
			}

			double v = ob1.ToBigNumber().ToDouble();
			double m = ob2.ToBigNumber().ToDouble();
			return TObject.GetDouble(v % m);
		}

	}
}