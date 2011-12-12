﻿// 
//  Copyright 2010-2011  Deveel
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
using System.Data;

using Deveel.Data.Client;
using Deveel.Diagnostics;

namespace Deveel.Data {
	public sealed partial class Database {
		/// <summary>
		/// Tries to authenticate a username/password against this database.
		/// </summary>
		/// <remarks>
		/// If a valid object is returned, the user will be logged into 
		/// the engine via the <see cref="Data.UserManager"/>. The developer must 
		/// ensure that <see cref="Dispose()"/> is called before the object is 
		/// disposed (logs out of the system).
		/// <para>
		/// This method also returns <b>null</b> if a user exists but was 
		/// denied access from the given host string. The given <i>host name</i>
		/// is formatted in the database host connection encoding. This 
		/// method checks all the values from the <see cref="SysUserPriv"/> 
		/// table for this user for the given protocol.
		/// It first checks if the user is specifically <b>denied</b> access 
		/// from the given host.It then checks if the user is <b>allowed</b> 
		/// access from the given host. If a host is neither allowed or denied 
		/// then it is denied.
		/// </para>
		/// </remarks>
		/// <returns>
		/// Returns a <see cref="User"/> object if the given user was authenticated 
		/// successfully, otherwise <b>null</b>.
		/// </returns>
		public User AuthenticateUser(String username, String password, String connection_string) {
			// Create a temporary connection for authentication only...
			DatabaseConnection connection = CreateNewConnection(null, null);
			DatabaseQueryContext context = new DatabaseQueryContext(connection);
			connection.CurrentSchema = SystemSchema;
			LockingMechanism locker = connection.LockingMechanism;
			locker.SetMode(LockingMode.Exclusive);
			try {
				try {
					IDbConnection conn = connection.GetDbConnection();

					// Is the username/password in the database?
					IDbCommand command = conn.CreateCommand();
					command.CommandText = " SELECT \"UserName\" FROM \"password\" " +
										  "  WHERE \"password.UserName\" = ? " +
										  "    AND \"password.Password\" = ? ";
					command.Parameters.Add(username);
					command.Parameters.Add(password);
					command.Prepare();

					IDataReader rs = command.ExecuteReader();
					if (!rs.Read())
						return null;
					rs.Close();

					// Now check if this user is permitted to connect from the given
					// host.
					if (UserAllowedAccessFromHost(context,
												  username, connection_string)) {
						// Successfully authenticated...
						User user = new User(username, this,
											connection_string, DateTime.Now);
						// Log the authenticated user in to the engine.
						system.UserManager.OnUserLoggedIn(user);
						return user;
					}

					return null;
				} catch (DataException e) {
					if (e is DbDataException) {
						DbDataException dbDataException = (DbDataException)e;
						Debug.Write(DebugLevel.Error, this, dbDataException.ServerErrorStackTrace);
					}
					Debug.WriteException(DebugLevel.Error, e);
					throw new Exception("SQL Error: " + e.Message);
				}
			} finally {
				try {
					// Make sure we commit the connection.
					connection.Commit();
				} catch (TransactionException e) {
					// Just issue a warning...
					Debug.WriteException(DebugLevel.Warning, e);
				} finally {
					// Guarentee that we unluck from EXCLUSIVE
					locker.FinishMode(LockingMode.Exclusive);
				}
				// And make sure we close (dispose) of the temporary connection.
				connection.Close();
			}
		}

		/// <summary>
		/// Performs check to determine if user is allowed access from the given
		/// host.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username">The name of the user to check the host for.</param>
		/// <param name="connection_string">The full connection string.</param>
		/// <returns>
		/// Returns <b>true</b> if the user identified by the given <paramref name="username"/>
		/// is allowed to access for the host specified in the <paramref name="connection_string"/>,
		/// otherwise <b>false</b>.
		/// </returns>
		private bool UserAllowedAccessFromHost(DatabaseQueryContext context, String username, String connection_string) {
			// The system user is not allowed to login
			if (username.Equals(InternalSecureUsername)) {
				return false;
			}

			// We always allow access from 'Internal/*' (connections from the
			// 'GetConnection' method of a com.mckoi.database.control.DbSystem object)
			// ISSUE: Should we add this as a rule?
			if (connection_string.StartsWith("Internal/")) {
				return true;
			}

			// What's the protocol?
			int protocol_host_deliminator = connection_string.IndexOf("/");
			String protocol =
				connection_string.Substring(0, protocol_host_deliminator);
			String host = connection_string.Substring(protocol_host_deliminator + 1);

			if (Debug.IsInterestedIn(DebugLevel.Information)) {
				Debug.Write(DebugLevel.Information, this,
							"Checking host: protocol = " + protocol +
							", host = " + host);
			}

			// The table to check
			DataTable connect_priv = context.GetTable(SysUserConnect);
			VariableName un_col = connect_priv.GetResolvedVariable(0);
			VariableName proto_col = connect_priv.GetResolvedVariable(1);
			VariableName host_col = connect_priv.GetResolvedVariable(2);
			VariableName access_col = connect_priv.GetResolvedVariable(3);
			// Query: where UserName = %username%
			Table t = connect_priv.SimpleSelect(context, un_col, Operator.Get("="),
												new Expression(TObject.CreateString(username)));
			// Query: where %protocol% like Protocol
			Expression exp = Expression.Simple(TObject.CreateString(protocol),
											   Operator.Get("like"), proto_col);
			t = t.ExhaustiveSelect(context, exp);
			// Query: where %host% like Host
			exp = Expression.Simple(TObject.CreateString(host),
									Operator.Get("like"), host_col);
			t = t.ExhaustiveSelect(context, exp);

			// Those that are DENY
			Table t2 = t.SimpleSelect(context, access_col, Operator.Get("="),
									  new Expression(TObject.CreateString("DENY")));
			if (t2.RowCount > 0) {
				return false;
			}
			// Those that are ALLOW
			Table t3 = t.SimpleSelect(context, access_col, Operator.Get("="),
									  new Expression(TObject.CreateString("ALLOW")));
			if (t3.RowCount > 0) {
				return true;
			}
			// No DENY or ALLOW entries for this host so deny access.
			return false;
		}

		/// <summary>
		/// Checks if a user exists within the database.
		/// </summary>
		/// <param name="context">The context of the session.</param>
		/// <param name="username">The name of the user to check.</param>
		/// <remarks>
		/// <b>Note:</b> Assumes exclusive Lock on the session.
		/// </remarks>
		/// <returns>
		/// Returns <b>true</b> if the user identified by the given 
		/// <paramref name="username"/>, otherwise <b>false</b>.
		/// </returns>
		public bool UserExists(DatabaseQueryContext context, String username) {
			DataTable table = context.GetTable(SysPassword);
			VariableName c1 = table.GetResolvedVariable(0);
			// All password where UserName = %username%
			Table t = table.SimpleSelect(context, c1, Operator.Get("="), new Expression(TObject.CreateString(username)));
			return t.RowCount > 0;
		}

		/// <summary>
		/// Creates a new user for the database.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username">The name of the user to create.</param>
		/// <param name="password">The user password.</param>
		/// <remarks>
		/// <b>Note</b>: Assumes exclusive Lock on <see cref="DatabaseConnection"/>.
		/// </remarks>
		/// <exception cref="DatabaseException">
		/// If the user is already defined by the database
		/// </exception>
		public void CreateUser(DatabaseQueryContext context, String username, String password) {
			if (username == null || password == null) {
				throw new DatabaseException("Username or password can not be NULL.");
			}

			// The username must be more than 1 character
			if (username.Length <= 1) {
				throw new DatabaseException("Username must be at least 2 characters.");
			}

			// The password must be more than 1 character
			if (password.Length <= 1) {
				throw new DatabaseException("Password must be at least 2 characters.");
			}

			// Check the user doesn't already exist
			if (UserExists(context, username)) {
				throw new DatabaseException("User '" + username + "' already exists.");
			}

			// Some usernames are reserved words
			if (String.Compare(username, "public", true) == 0) {
				throw new DatabaseException("User '" + username +
											"' not allowed - reserved.");
			}

			// Usernames starting with @, &, # and $ are reserved for system
			// identifiers
			char c = username[0];
			if (c == '@' || c == '&' || c == '#' || c == '$') {
				throw new DatabaseException("User name can not start with '" + c +
											"' character.");
			}

			// Add this user to the password table.
			DataTable table = context.GetTable(SysPassword);
			DataRow rdat = new DataRow(table);
			rdat.SetValue(0, username);
			rdat.SetValue(1, password);
			table.Add(rdat);
		}

		/// <summary>
		/// Deletes all the groups the user belongs to.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username"></param>
		/// <remarks>
		/// This is intended for a user alter command for setting the groups 
		/// a user belongs to.
		/// <para>
		/// <b>Note:</b> Assumes exclusive Lock on database session.
		/// </para>
		/// </remarks>
		public void DeleteAllUserGroups(DatabaseQueryContext context, String username) {
			Operator EQUALS_OP = Operator.Get("=");
			Expression USER_EXPR = new Expression(TObject.CreateString(username));

			DataTable table = context.GetTable(SysUserPriv);
			VariableName c1 = table.GetResolvedVariable(0);
			// All 'user_priv' where UserName = %username%
			Table t = table.SimpleSelect(context, c1, EQUALS_OP, USER_EXPR);
			// Delete all the groups
			table.Delete(t);
		}

		/// <summary>
		/// Drops a user from the database.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username">The name of the user to drop.</param>
		/// <remarks>
		/// This also deletes all information associated with a user such as 
		/// the groups they belong to. It does not delete the privs a user 
		/// has set up.
		/// <para>
		/// <b>Note:</b> Assumes exclusive Lock on database session.
		/// </para>
		/// </remarks>
		public void DeleteUser(DatabaseQueryContext context, String username) {
			// PENDING: This should check if there are any tables the user has setup
			//  and not allow the delete if there are.

			Operator EQUALS_OP = Operator.Get("=");
			Expression USER_EXPR = new Expression(TObject.CreateString(username));

			// First delete all the groups from the user priv table
			DeleteAllUserGroups(context, username);

			// Now delete the username from the user_connect_priv table
			DataTable table = context.GetTable(SysUserConnect);
			VariableName c1 = table.GetResolvedVariable(0);
			Table t = table.SimpleSelect(context, c1, EQUALS_OP, USER_EXPR);
			table.Delete(t);

			// Finally delete the username from the 'password' table
			table = context.GetTable(SysPassword);
			c1 = table.GetResolvedVariable(0);
			t = table.SimpleSelect(context, c1, EQUALS_OP, USER_EXPR);
			table.Delete(t);
		}

		/// <summary>
		/// Alters the password of the given user.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username">The name of the user to alter the password.</param>
		/// <param name="password">The new password for the user.</param>
		/// <remarks>
		/// <b>Note:</b> Assumes exclusive Lock on database session.
		/// </remarks>
		public void AlterUserPassword(DatabaseQueryContext context, String username, String password) {
			Operator EQUALS_OP = Operator.Get("=");
			Expression USER_EXPR = new Expression(TObject.CreateString(username));

			// Delete the current username from the 'password' table
			DataTable table = context.GetTable(SysPassword);
			VariableName c1 = table.GetResolvedVariable(0);
			Table t = table.SimpleSelect(context, c1, EQUALS_OP, USER_EXPR);
			if (t.RowCount == 1) {
				table.Delete(t);

				// Add the new username
				table = context.GetTable(SysPassword);
				DataRow rdat = new DataRow(table);
				rdat.SetValue(0, username);
				rdat.SetValue(1, password);
				table.Add(rdat);
			} else {
				throw new DatabaseException("Username '" + username + "' was not found.");
			}
		}

		/// <summary>
		/// Returns the list of all user groups the user belongs to.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username"></param>
		/// <returns></returns>
		public String[] GroupsUserBelongsTo(DatabaseQueryContext context, String username) {
			DataTable table = context.GetTable(SysUserPriv);
			VariableName c1 = table.GetResolvedVariable(0);
			// All 'user_priv' where UserName = %username%
			Table t = table.SimpleSelect(context, c1, Operator.Get("="),
										 new Expression(TObject.CreateString(username)));
			int sz = t.RowCount;
			string[] groups = new string[sz];
			IRowEnumerator row_enum = t.GetRowEnumerator();
			int i = 0;
			while (row_enum.MoveNext()) {
				groups[i] = t.GetCellContents(1, row_enum.RowIndex).Object.ToString();
				++i;
			}

			return groups;
		}

		/// <summary>
		/// Checks if a user belongs in a specified group.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username">The name of the user to check.</param>
		/// <param name="group">The name of the group to check.</param>
		/// <remarks>
		/// <b>Note</b> Assumes exclusive Lock on database session.
		/// </remarks>
		/// <returns>
		/// Returns <b>true</b> if the given user belongs to the given
		/// <paramref name="group"/>, otherwise <b>false</b>.
		/// </returns>
		public bool UserBelongsToGroup(DatabaseQueryContext context,
									   String username, String group) {
			DataTable table = context.GetTable(SysUserPriv);
			VariableName c1 = table.GetResolvedVariable(0);
			VariableName c2 = table.GetResolvedVariable(1);
			// All 'user_priv' where UserName = %username%
			Table t = table.SimpleSelect(context, c1, Operator.Get("="),
										 new Expression(TObject.CreateString(username)));
			// All from this set where PrivGroupName = %group%
			t = t.SimpleSelect(context, c2, Operator.Get("="),
							   new Expression(TObject.CreateString(group)));
			return t.RowCount > 0;
		}

		/// <summary>
		/// Adds a user to the given group.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="username">The name of the user to be added.</param>
		/// <param name="group">The name of the group to add the user to.</param>
		/// <remarks>
		/// This makes an entry in the <see cref="SysUserPriv"/> for this user 
		/// and the given group.
		/// If the user already belongs to the group then no changes are made.
		/// <para>
		/// It is important that any security checks for ensuring the grantee is
		/// allowed to give the user these privileges are preformed before this 
		/// method is called.
		/// </para>
		/// <para>
		/// <b>Note</b> Assumes exclusive Lock on database session.
		/// </para>
		/// </remarks>
		/// <exception cref="DatabaseException">
		/// If the group name is not properly formatted.
		/// </exception>
		public void AddUserToGroup(DatabaseQueryContext context,
								   String username, String group) {
			if (group == null) {
				throw new DatabaseException("Can add NULL group.");
			}

			// Groups starting with @, &, # and $ are reserved for system
			// identifiers
			char c = group[0];
			if (c == '@' || c == '&' || c == '#' || c == '$') {
				throw new DatabaseException("The group name can not start with '" + c +
											"' character.");
			}

			// Check the user doesn't belong to the group
			if (!UserBelongsToGroup(context, username, group)) {
				// The user priv table
				DataTable table = context.GetTable(SysUserPriv);
				// Add this user to the group.
				DataRow rdat = new DataRow(table);
				rdat.SetValue(0, username);
				rdat.SetValue(1, group);
				table.Add(rdat);
			}
			// NOTE: we silently ignore the case when a user already belongs to the
			//   group.
		}

		/// <summary>
		/// Sets the Lock status for the given user.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user">The user to set the Lock status.</param>
		/// <param name="lock_status"></param>
		/// <remarks>
		/// If a user account if locked, it is rejected from logging in 
		/// to the database.
		/// <para>
		/// It is important that any security checks to determine if the process
		/// setting the user Lock is allowed to do it is done before this method is
		/// called.
		/// </para>
		/// <para>
		/// <b>Note:</b> Assumes exclusive Lock on database session.
		/// </para>
		/// </remarks>
		public void SetUserLock(DatabaseQueryContext context, User user, bool lock_status) {
			String username = user.UserName;

			// Internally we implement this by adding the user to the #locked group.
			DataTable table = context.GetTable(SysUserPriv);
			VariableName c1 = table.GetResolvedVariable(0);
			VariableName c2 = table.GetResolvedVariable(1);
			// All 'user_priv' where UserName = %username%
			Table t = table.SimpleSelect(context, c1, Operator.Get("="),
										 new Expression(TObject.CreateString(username)));
			// All from this set where PrivGroupName = %group%
			t = t.SimpleSelect(context, c2, Operator.Get("="),
							   new Expression(TObject.CreateString(LockGroup)));

			bool user_belongs_to_lock_group = t.RowCount > 0;
			if (lock_status && !user_belongs_to_lock_group) {
				// Lock the user by adding the user to the Lock group
				// Add this user to the locked group.
				DataRow rdat = new DataRow(table);
				rdat.SetValue(0, username);
				rdat.SetValue(1, LockGroup);
				table.Add(rdat);
			} else if (!lock_status && user_belongs_to_lock_group) {
				// Unlock the user by removing the user from the Lock group
				// Remove this user from the locked group.
				table.Delete(t);
			}
		}

		/// <summary>
		/// Grants the given user access to connect to the database from the
		/// given host address.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user">The name of the user to grant the access to.</param>
		/// <param name="protocol">The connection protocol (either <i>TCP</i> or <i>Local</i>).</param>
		/// <param name="host">The connection host.</param>
		/// <remarks>
		/// We look forward to support more protocols.
		/// </remarks>
		/// <exception cref="DatabaseException">
		/// If the given <paramref name="protocol"/> is not <i>TCP</i> or 
		/// <i>Local</i>.
		/// </exception>
		public void GrantHostAccessToUser(DatabaseQueryContext context,
										  String user, String protocol, String host) {
			// The user connect priv table
			DataTable table = context.GetTable(SysUserConnect);
			// Add the protocol and host to the table
			DataRow rdat = new DataRow(table);
			rdat.SetValue(0, user);
			rdat.SetValue(1, protocol);
			rdat.SetValue(2, host);
			rdat.SetValue(3, "ALLOW");
			table.Add(rdat);
		}

		/// <summary>
		/// Checks if the given user belongs to secure group.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <returns>
		/// Returns <b>true</b> if the user belongs to the secure access
		/// privileges group, otherwise <b>false</b>.
		/// </returns>
		private bool UserHasSecureAccess(DatabaseQueryContext context, User user) {
			// The internal secure user has full privs on everything
			if (user.UserName.Equals(InternalSecureUsername)) {
				return true;
			}
			return UserBelongsToGroup(context, user.UserName, SecureGroup);
		}

		/// <summary>
		/// Checks if the given user is permitted the given grant for
		/// executing operations on the given schema.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="schema"></param>
		/// <param name="grant"></param>
		/// <returns>
		/// Returns <b>true</b> if the grant manager permits a schema 
		/// operation (eg, <i>CREATE</i>, <i>ALTER</i> and <i>DROP</i> 
		/// table operations) for the given user, otherwise <b>false</b>.
		/// </returns>
		private static bool UserHasSchemaGrant(DatabaseQueryContext context,
											   User user, String schema, int grant) {
			// The internal secure user has full privs on everything
			if (user.UserName.Equals(InternalSecureUsername)) {
				return true;
			}

			// No users have schema access to the system schema.
			if (schema.Equals(SystemSchema)) {
				return false;
			}

			// Ask the grant manager if there are any privs setup for this user on the
			// given schema.
			GrantManager manager = context.GrantManager;
			Privileges privs = manager.GetUserGrants(
				GrantObject.Schema, schema, user.UserName);

			return privs.Permits(grant);
		}

		/// <summary>
		/// Checks if the given user is permitted the given grant for
		/// executing operations on the given table.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="table_name"></param>
		/// <param name="columns"></param>
		/// <param name="grant"></param>
		/// <returns>
		/// Returns <b>true</b> if the grant manager permits a schema 
		/// operation (eg, <c>CREATE</c>, <c>ALTER</c> and <c>DROP</c> 
		/// table operations) for the given user, otherwise <b>false</b>.
		/// </returns>
		private static bool UserHasTableObjectGrant(DatabaseQueryContext context,
													User user, TableName table_name, VariableName[] columns,
													int grant) {
			// The internal secure user has full privs on everything
			if (user.UserName.Equals(InternalSecureUsername)) {
				return true;
			}

			// PENDING: Support column level privileges.

			// Ask the grant manager if there are any privs setup for this user on the
			// given schema.
			GrantManager manager = context.GrantManager;
			Privileges privs = manager.GetUserGrants(
				GrantObject.Table, table_name.ToString(), user.UserName);

			return privs.Permits(grant);
		}

		/// <summary>
		/// Checks if the given user can create users on the database.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <remarks>
		/// Only members of the <i>secure access</i> group, or the 
		/// <i>user manager</i> group can do this.
		/// </remarks>
		/// <returns>
		/// Returns <b>true</b> if the user is permitted to create, 
		/// alter and drop user information from the database, otherwise 
		/// returns <b>false</b>.
		/// </returns>
		public bool CanUserCreateAndDropUsers(
			DatabaseQueryContext context, User user) {
			return (UserHasSecureAccess(context, user) ||
					UserBelongsToGroup(context, user.UserName,
									   UserManagerGroup));
		}

		/// <summary>
		/// Returns true if the user is permitted to create and drop schema's in the
		/// database, otherwise returns false.
		/// </summary>
		/// <remarks>
		/// Only members of the 'secure access' group, or the 'schema manager' group 
		/// can do this.
		/// </remarks>
		public bool CanUserCreateAndDropSchema(DatabaseQueryContext context, User user, String schema) {
			// The internal secure user has full privs on everything
			if (user.UserName.Equals(InternalSecureUsername)) {
				return true;
			}

			// No user can create or drop the system schema.
			if (schema.Equals(SystemSchema)) {
				return false;
			} else {
				return (UserHasSecureAccess(context, user) ||
						UserBelongsToGroup(context, user.UserName,
										   SchemaManagerGroup));
			}
		}

		/// <summary>
		/// Returns true if the user can shut down the database server.
		/// </summary>
		/// <remarks>
		/// A user can shut down the database if they are a member of the 
		/// 'secure acces' group.
		/// </remarks>
		public bool CanUserShutDown(DatabaseQueryContext context, User user) {
			return UserHasSecureAccess(context, user);
		}

		/// <summary>
		/// Returns true if the user is allowed to execute the given stored procedure.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="procedure_name"></param>
		/// <returns></returns>
		public bool CanUserExecuteStoredProcedure(DatabaseQueryContext context,
												  User user, String procedure_name) {
			// Currently you can only execute a procedure if you are a member of the
			// secure access priv group.
			return UserHasSecureAccess(context, user);
		}

		// ---- General schema level privs ----

		/// <summary>
		/// Returns true if the user can create a table or view with the given name,
		/// otherwise returns false.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		public bool CanUserCreateTableObject(DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user, table.Schema, Privileges.Create)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		/// <summary>
		/// Returns true if the user can alter a table or view with the given name,
		/// otherwise returns false.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		public bool CanUserAlterTableObject(DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user, table.Schema, Privileges.Alter))
				return true;

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		/// <summary>
		/// Returns true if the user can drop a table or view with the given name,
		/// otherwise returns false.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		public bool CanUserDropTableObject(DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user, table.Schema, Privileges.Drop)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		// ---- Check table object privs ----

		/// <summary>
		/// Returns true if the user can select from a table or view with the given
		/// name and given columns, otherwise returns false.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="table"></param>
		/// <param name="columns"></param>
		/// <returns></returns>
		public bool CanUserSelectFromTableObject(
			DatabaseQueryContext context, User user, TableName table,
			VariableName[] columns) {
			if (UserHasTableObjectGrant(context, user, table, columns,
										Privileges.Select)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		/// <summary>
		///  Returns true if the user can insert into a table or view with the given
		/// name and given columns, otherwise returns false.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="user"></param>
		/// <param name="table"></param>
		/// <param name="columns"></param>
		/// <returns></returns>
		public bool CanUserInsertIntoTableObject(
			DatabaseQueryContext context, User user, TableName table,
			VariableName[] columns) {
			if (UserHasTableObjectGrant(context, user, table, columns,
										Privileges.Insert)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		/// <summary>
		/// Returns true if the user can update a table or view with the given
		/// name and given columns, otherwise returns false.
		/// </summary>
		public bool CanUserUpdateTableObject(
			DatabaseQueryContext context, User user, TableName table,
			VariableName[] columns) {
			if (UserHasTableObjectGrant(context, user, table, columns,
										Privileges.Update)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		///<summary>
		/// Returns true if the user can delete from a table or view with the given
		/// name and given columns, otherwise returns false.
		///</summary>
		///<param name="context"></param>
		///<param name="user"></param>
		///<param name="table"></param>
		///<returns></returns>
		public bool CanUserDeleteFromTableObject(
			DatabaseQueryContext context, User user, TableName table) {
			if (UserHasTableObjectGrant(context, user, table, null,
										Privileges.Delete)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		///<summary>
		/// Returns true if the user can compact a table with the given name,
		/// otherwise returns false.
		///</summary>
		///<param name="context"></param>
		///<param name="user"></param>
		///<param name="table"></param>
		///<returns></returns>
		public bool CanUserCompactTableObject(
			DatabaseQueryContext context, User user, TableName table) {
			if (UserHasTableObjectGrant(context, user, table, null,
										Privileges.Compact)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		///<summary>
		/// Returns true if the user can create a procedure with the given name,
		/// otherwise returns false.
		///</summary>
		///<param name="context"></param>
		///<param name="user"></param>
		///<param name="table"></param>
		///<returns></returns>
		public bool CanUserCreateProcedureObject(
			DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user,
								   table.Schema, Privileges.Create)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		///<summary>
		/// Returns true if the user can drop a procedure with the given name,
		/// otherwise returns false.
		///</summary>
		///<param name="context"></param>
		///<param name="user"></param>
		///<param name="table"></param>
		///<returns></returns>
		public bool CanUserDropProcedureObject(
			DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user,
								   table.Schema, Privileges.Drop)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		///<summary>
		///  Returns true if the user can create a sequence with the given name,
		/// otherwise returns false.
		///</summary>
		///<param name="context"></param>
		///<param name="user"></param>
		///<param name="table"></param>
		///<returns></returns>
		public bool CanUserCreateSequenceObject(
			DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user,
								   table.Schema, Privileges.Create)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}

		///<summary>
		/// Returns true if the user can drop a sequence with the given name,
		/// otherwise returns false.
		///</summary>
		///<param name="context"></param>
		///<param name="user"></param>
		///<param name="table"></param>
		///<returns></returns>
		public bool CanUserDropSequenceObject(
			DatabaseQueryContext context, User user, TableName table) {
			if (UserHasSchemaGrant(context, user,
								   table.Schema, Privileges.Drop)) {
				return true;
			}

			// If the user belongs to the secure access priv group, return true
			return UserHasSecureAccess(context, user);
		}
 
	}
}