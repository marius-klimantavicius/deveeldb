// 
//  DbSystem.cs
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
using System.Data;
using System.Text;

using Deveel.Data.Client;
using Deveel.Data.Server;
using Deveel.Diagnostics;

namespace Deveel.Data.Control {
	///<summary>
	/// An object used to access and control a single database system running 
	/// in the current runtime.
	///</summary>
	/// <remarks>
	/// This object provides various access methods to safely manipulate the 
	/// database, as well as allowing server plug-ins.  For example, a TCP/IP 
	/// server component might be plugged into this object to open the database 
	/// to remote access.
	/// </remarks>
	public sealed class DbSystem {
		/// <summary>
		/// The DbController object.
		/// </summary>
		private DbController controller;

		/// <summary>
		/// The <see cref="IDbConfig"/> object that describes the startup configuration 
		/// of the database.
		/// </summary>
		private IDbConfig config;

		/// <summary>
		/// The underlying <see cref="Data.Database"/> object of this system.
		/// </summary>
		/// <remarks>
		/// This object gives low level access to the system.
		/// </remarks>
		private Database database;

		/// <summary>
		/// An internal counter for internal connections created on this system.
		/// </summary>
		private int internal_counter;


		internal DbSystem(DbController controller, IDbConfig config, Database database) {
			this.controller = controller;
			this.config = config;
			this.database = database;
			internal_counter = 0;

			// Register the shut down delegate,
			database.RegisterShutDownDelegate(new EventHandler(Shutdown));

			// Enable commands to the database system...
			database.SetIsExecutingCommands(true);

		}

		private void Shutdown(object sender, EventArgs e) {
			InternalDispose();
		}

		///<summary>
		/// Returns an immutable version of the database system configuration.
		///</summary>
		public IDbConfig Config {
			get { return config; }
		}

		// ---------- Internal access methods ----------

		///<summary>
		/// Returns the <see cref="Database"/> object for this control that can be used
		/// to access the database system at a low level.
		///</summary>
		/// <remarks>
		/// This property only works correctly if the database engine has successfully
		/// been initialized.
		/// <para>
		/// This object is generally not very useful unless you intend to perform some 
		/// sort of low level function on the database.  This object can be used to bypass 
		/// the SQL layer and talk directly with the internals of the database.
		/// </para>
		/// </remarks>
		public Database Database {
			get { return database; }
		}

		///<summary>
		/// Makes a connection to the database and returns a <see cref="IDbConnection"/> 
		/// object that can be used to execute queries on the database.
		///</summary>
		///<param name="schema">The initial database schema to start the connection in.</param>
		///<param name="username">The user to login to the database under.</param>
		///<param name="password">The password of the user.</param>
		/// <remarks>
		/// This is a standard connection that talks directly with the database without 
		/// having to go through any communication protocol layers.
		/// <para>
		/// For example, if this control is for a database server, the <see cref="IDbConnection"/>
		/// returned here does not go through the TCP/IP connection.  For this reason certain database 
		/// configuration constraints (such as number of concurrent connection on the database) may not 
		/// apply to this connection.
		/// </para>
		/// </remarks>
		///<returns>
		/// Returns a <see cref="IDbConnection"/> instance used to access the database.
		/// </returns>
		/// <exception cref="DataException">
		/// Thrown if the login fails with the credentials given.
		/// </exception>
		public IDbConnection GetConnection(String schema, String username, String password) {
			// Create the host string, formatted as 'Internal/[hash number]/[counter]'
			StringBuilder buf = new StringBuilder();
			buf.Append("Internal/");
			buf.Append(GetHashCode());
			buf.Append('/');
			lock (this) {
				buf.Append(internal_counter);
				++internal_counter;
			}
			String host_string = buf.ToString();

			// Create the database interface for an internal database connection.
			IDatabaseInterface db_interface =
							   new DatabaseInterface(Database, host_string);
			// Create the DbConnection object (very minimal cache settings for an
			// internal connection).
			DbConnection connection = new DbConnection("", db_interface, 8, 4092000);
			// Attempt to log in with the given username and password (default schema)
			connection.Login(schema, username, password);

			// And return the new connection
			return connection;
		}

		///<summary>
		/// Makes a connection to the database and returns a <see cref="IDbConnection"/> 
		/// object that can be used to execute queries on the database.
		///</summary>
		///<param name="username">The user to login to the database under.</param>
		///<param name="password">The password of the user.</param>
		/// <remarks>
		/// This is a standard connection that talks directly with the database without 
		/// having to go through any communication protocol layers.
		/// <para>
		/// For example, if this control is for a database server, the <see cref="IDbConnection"/>
		/// returned here does not go through the TCP/IP connection.  For this reason certain database 
		/// configuration constraints (such as number of concurrent connection on the database) may not 
		/// apply to this connection.
		/// </para>
		/// </remarks>
		///<returns>
		/// Returns a <see cref="IDbConnection"/> instance used to access the database.
		/// </returns>
		/// <exception cref="DataException">
		/// Thrown if the login fails with the credentials given.
		/// </exception>
		public IDbConnection GetConnection(String username, String password) {
			return GetConnection(null, username, password);
		}

		// ---------- Global methods ----------

		///<summary>
		/// Sets a flag that causes the database to delete itself from the file 
		/// system when it is shut down.
		///</summary>
		///<param name="status"></param>
		/// <remarks>
		/// This is useful if an application needs a temporary database to work 
		/// with that is released from the file system when the application ends.
		/// <para>
		/// By default, a database is not deleted from the file system when it is
		/// closed.
		/// </para>
		/// <para>
		/// <b>Note</b>: Use with care - setting this flag will cause all data stored 
		/// in the database to be lost when the database is shut down.
		/// </para>
		/// </remarks>
		public void SetDeleteOnClose(bool status) {
			database.SetDeleteOnShutdown(status);
		}

		///<summary>
		/// Closes this database system so it is no longer able to process queries.
		///</summary>
		/// <remarks>
		/// A database may be shut down either through this method or by executing a 
		/// query that shuts the system down (for example, <c>SHUTDOWN</c>).
		/// <para>
		/// When a database system is closed, it is not able to be restarted again
		/// unless a new <see cref="DbSystem"/> object is obtained from the <see cref="DbController"/>.
		/// </para>
		/// <para>
		/// This method also disposes all resources associated with the database 
		/// system (such as threads, etc) so that it may be reclaimed by the garbage 
		/// collector.
		/// </para>
		/// <para>
		/// When this method returns this object is no longer usable.
		/// </para>
		/// </remarks>
		public void Close() {
			if (database != null) {
				database.StartShutDownThread();
				database.WaitUntilShutdown();
			}
		}

		// ---------- Private methods ----------

		/// <summary>
		/// Disposes of all the resources associated with this system.
		/// </summary>
		/// <remarks>
		/// It may only be called from the shutdown delegate registered 
		/// in the constructor.
		/// </remarks>
		private void InternalDispose() {
			if (database != null && database.IsInitialized) {

				// Disable commands (on worker threads) to the database system...
				database.SetIsExecutingCommands(false);

				try {
					database.Shutdown();
				} catch (DatabaseException e) {
					Debug.Write(DebugLevel.Error, this, "Unable to shutdown database because of exception");
					Debug.WriteException(DebugLevel.Error, e);
				}
			}
			controller = null;
			config = null;
			database = null;
		}

	}
}