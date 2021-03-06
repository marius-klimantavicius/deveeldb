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

namespace Deveel.Data.Sql.Triggers {
	public static class ContextExtensions {
		public static void DeclareTrigger(this IContext context, TriggerInfo triggerInfo) {
			var current = context;
			while (current != null) {
				if (current is ITriggerScope) {
					var scope = (ITriggerScope) current;
					scope.TriggerManager.CreateTrigger(triggerInfo);
					return;
				}

				current = current.Parent;
			}

			throw new InvalidOperationException("No trigger scope found in context");
		}

		public static bool DropTrigger(this IContext context, string triggerName) {
			var current = context;
			while (current != null) {
				if (current is ITriggerScope) {
					var scope = (ITriggerScope) current;
					if (scope.TriggerManager.DropTrigger(new ObjectName(triggerName)))
						return true;
				}

				current = current.Parent;
			}

			return false;
		}

		public static bool TriggerExists(this IContext context, string triggerName) {
			var current = context;
			while (current != null) {
				if (current is ITriggerScope) {
					var scope = (ITriggerScope)current;
					return scope.TriggerManager.TriggerExists(ObjectName.Parse(triggerName));
				}

				current = current.Parent;
			}

			return false;
		}

		public static Trigger FindTrigger(this IContext context, string triggerName) {
			var current = context;
			while (current != null) {
				if (current is ITriggerScope) {
					var scope = (ITriggerScope)current;
					return scope.TriggerManager.GetTrigger(ObjectName.Parse(triggerName));
				}

				current = current.Parent;
			}

			return null;
		}

		public static void FireTriggers(this IContext context, IRequest request, TableEvent tableEvent) {
			var current = context;
			while (current != null) {
				if (current is ITriggerScope) {
					var scope = (ITriggerScope)current;
					scope.TriggerManager.FireTriggers(request, tableEvent);
				}

				current = current.Parent;
			}
		}
	}
}
