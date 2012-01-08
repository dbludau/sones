/*
* sones GraphDB - Community Edition - http://www.sones.com
* Copyright (C) 2007-2011 sones GmbH
*
* This file is part of sones GraphDB Community Edition.
*
* sones GraphDB is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as published by
* the Free Software Foundation, version 3 of the License.
* 
* sones GraphDB is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with sones GraphDB. If not, see <http://www.gnu.org/licenses/>.
* 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IdentityModel.Selectors;
using sones.GraphDSServer;
using System.IdentityModel.Tokens;
using System.Diagnostics;
using sones.GraphDB;
using sones.Library.VersionedPluginManager;
using sones.GraphDS.PluginManager;
using sones.Library.Commons.Security;
using System.Net;
using System.Threading;
using sones.GraphDB.Manager.Plugin;
using System.IO;
using System.Globalization;
using sones.Library.DiscordianDate;
using System.Security.AccessControl;
using sones.GraphDSServer.ErrorHandling;


namespace sones.sonesGraphDBStarter
{
	#region PassValidator
	public class PassValidator : UserNamePasswordValidator
	{
		public override void Validate(String myUserName, String myPassword)
		{

			Debug.WriteLine(String.Format("Authenticate {0} and {1}", myUserName, myPassword));

			if (!(myUserName == Properties.Settings.Default.Username && myPassword == Properties.Settings.Default.Password))
			{
				throw new SecurityTokenException("Unknown Username or Password");
			}

		}
	}
	#endregion

	#region sones GraphDB Startup
	public class sonesGraphDBStartup
	{
		private bool quiet = false;
		private bool shutdown = false;
		private IGraphDSServer _dsServer;
		private bool _ctrlCPressed;

		public sonesGraphDBStartup(String[] myArgs)
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(Properties.Settings.Default.DatabaseCulture);

			if (myArgs.Count() > 0)
			{
				foreach (String parameter in myArgs)
				{
					if (parameter.ToUpper() == "--Q")
						quiet = true;
				}
			}
			#region Start REST, WebDAV and WebAdmin services, send GraphDS notification
			
			IGraphDB GraphDB;

			if (Properties.Settings.Default.UsePersistence)
			{
				if (!quiet)
				   Console.WriteLine("Initializing persistence layer...");
 
				Uri configuredLocation = new Uri(Properties.Settings.Default.PersistenceLocation, UriKind.RelativeOrAbsolute);
				string configuredPageSize = Properties.Settings.Default.PageSize;
				string configuredBufferSize = Properties.Settings.Default.BufferSizeInPages;
				string configuredUseVertexExtensions = Properties.Settings.Default.UseVertexExtensions;
				string configuredWriteStrategy = Properties.Settings.Default.WriteStrategy;
				/* Configure the location */

				Uri location = null;

				if (configuredLocation.IsAbsoluteUri)
				{
					location = configuredLocation;
				}
				else
				{
					Uri rootPath = new Uri(System.Reflection.Assembly.GetAssembly((typeof(sones.Library.Commons.VertexStore.IVertexStore))).Location);
					location = new Uri(rootPath, configuredLocation);
				}

				 /* Configuration for the page size */
				int pageSize = Int32.Parse(configuredPageSize);

				/* Configuration for the buffer size */
				int bufferSize = Int32.Parse(configuredBufferSize);

				/* Configuration for using vertex extensions */
				bool useVertexExtensions = Boolean.Parse(configuredUseVertexExtensions);

				/* Make a new instance by applying the configuration */
				try
				{
					//Make a new GraphDB instance
					 GraphDB = new SonesGraphDB(new GraphDBPlugins(
						new PluginDefinition("sones.pagedfsnonrevisionedplugin", new Dictionary<string, object>() { { "location", location },
																													{ "pageSize", pageSize },
																													{ "bufferSizePages", bufferSize },
																													{ "writeStrategy", configuredWriteStrategy },
																													{ "useVertexExtensions", useVertexExtensions } })), true, null, location.AbsolutePath);


					if (!quiet)
						Console.WriteLine("Persistence layer initialized.");
				}
				catch (Exception a)
				{
					if (!quiet)
					{ 
						Console.WriteLine(a.Message);
						Console.WriteLine(a.StackTrace);

						Console.Error.WriteLine("Could not access the data directory " + location.AbsoluteUri + ". Please make sure you that you have the right file access permissions!");
						Console.Error.WriteLine("Using in memory storage instead.");
					}
					GraphDB = new SonesGraphDB(null,true,new CultureInfo(Properties.Settings.Default.DatabaseCulture));
				}
			}
			else
			{
				GraphDB = new SonesGraphDB(null, true, new CultureInfo(Properties.Settings.Default.DatabaseCulture));
			}

			#region Configure PlugIns
			// Plugins are loaded by the GraphDS with their according PluginDefinition and only if they are listed
			// below - there is no auto-discovery for plugin types in GraphDS (!)

				#region Query Languages
				// the GQL Query Language Plugin needs the GraphDB instance as a parameter
				List<PluginDefinition> QueryLanguages = new List<PluginDefinition>();
				Dictionary<string, object> GQL_Parameters = new Dictionary<string, object>();
				GQL_Parameters.Add("GraphDB", GraphDB);

				QueryLanguages.Add(new PluginDefinition("sones.gql", GQL_Parameters));
				#endregion

				#region REST Service Plugins
				List<PluginDefinition> SonesRESTServices = new List<PluginDefinition>();
				// not yet used
				#endregion

				#region GraphDS Service Plugins
				List<PluginDefinition> GraphDSServices = new List<PluginDefinition>();
				#endregion

				#region Drain Pipes            
				
				//// QueryLog DrainPipe
				//Dictionary<string, object> QueryLog_Parameters = new Dictionary<string, object>();
				//QueryLog_Parameters.Add("AsynchronousMode", true);  // do the work in a separate thread to not slow down queries
				//QueryLog_Parameters.Add("MaximumAsyncBufferSize", (Int32)1024 * 1024 * 10); // 10 Mbytes of maximum async queue size
				//QueryLog_Parameters.Add("AppendLogPathAndName", "sones.drainpipelog");
				//QueryLog_Parameters.Add("CreateNew", false); // always create a new file on start-up
				//QueryLog_Parameters.Add("FlushOnWrite", true);  // always flush on each write
			
				//// the DrainPipe Log expects several parameters
				//Dictionary<string, object> DrainPipeLog_Parameters = new Dictionary<string, object>();
				//DrainPipeLog_Parameters.Add("AsynchronousMode", true);  // do the work in a separate thread to not slow down queries
				//DrainPipeLog_Parameters.Add("MaximumAsyncBufferSize", (Int32)1024 * 1024 * 10); // 10 Mbytes of maximum async queue size
				//DrainPipeLog_Parameters.Add("AppendLogPathAndName", "sones.drainpipelog");
				//DrainPipeLog_Parameters.Add("CreateNew", false); // always create a new file on start-up
				//DrainPipeLog_Parameters.Add("FlushOnWrite", true);  // always flush on each write

				//Dictionary<string, object> DrainPipeLog2_Parameters = new Dictionary<string, object>();
				//DrainPipeLog2_Parameters.Add("AsynchronousMode", true);  // do the work in a separate thread to not slow down queries
				//DrainPipeLog2_Parameters.Add("MaximumAsyncBufferSize", (Int32)1024 * 1024 * 10); // 10 Mbytes of maximum async queue size
				//DrainPipeLog2_Parameters.Add("AppendLogPathAndName", "sones.drainpipelog2");
				//DrainPipeLog2_Parameters.Add("CreateNew", false); // always create a new file on start-up
				//DrainPipeLog2_Parameters.Add("FlushOnWrite", true);  // always flush on each write


				List<PluginDefinition> DrainPipes = new List<PluginDefinition>();
				//DrainPipes.Add(new PluginDefinition("sones.querylog", QueryLog_Parameters));
				//DrainPipes.Add(new PluginDefinition("sones.drainpipelog", DrainPipeLog_Parameters));
				//DrainPipes.Add(new PluginDefinition("sones.drainpipelog", DrainPipeLog2_Parameters));
				#endregion
				List<PluginDefinition> UsageDataCollector = new List<PluginDefinition>();

				#region UsageDataCollector
				if (Properties.Settings.Default.UDCEnabled)
				{
					Dictionary<string, object> UDC_parameters = new Dictionary<string, object>();
					UDC_parameters.Add("UDCWaitUpfrontTime", (Int32)Properties.Settings.Default.UDCWaitUpfront);  // do the work in a separate thread to not slow down queries
					UDC_parameters.Add("UDCUpdateInterval", (Int32)Properties.Settings.Default.UDCUpdateInterval); // 10
					UsageDataCollector.Add(new PluginDefinition("sones.GraphDS.UsageDataCollectorClient",UDC_parameters));
				}                
				#endregion

			#endregion

			GraphDSPlugins PluginsAndParameters = new GraphDSPlugins(QueryLanguages, DrainPipes,UsageDataCollector);
			_dsServer = new GraphDS_Server(GraphDB, PluginsAndParameters);

			#region Start GraphDS Services

			#region pre-configure REST Service
			Dictionary<string, object> RestParameter = new Dictionary<string, object>();
			RestParameter.Add("IPAddress", System.Net.IPAddress.Any);
			RestParameter.Add("Port", Properties.Settings.Default.ListeningPort);
			RestParameter.Add("Username", Properties.Settings.Default.Username);
			RestParameter.Add("Password", Properties.Settings.Default.Password);
			_dsServer.StartService("sones.RESTService", RestParameter);
			#endregion

			#region Remote API Service

			Dictionary<string, object> RemoteAPIParameter = new Dictionary<string, object>();
			RemoteAPIParameter.Add("IPAddress", System.Net.IPAddress.Any);
			RemoteAPIParameter.Add("Port", (ushort)9970);
			//RemoteAPIParameter.Add("IsSecure", true);

			_dsServer.StartService("sones.RemoteAPIService", RemoteAPIParameter);
			#endregion


			#endregion

			_dsServer.LogOn(new UserPasswordCredentials(Properties.Settings.Default.Username,Properties.Settings.Default.Password));
						
			#endregion

			#region Some helping lines...
			if (!quiet)
			{
				Console.WriteLine("This GraphDB Instance offers the following options:");
				Console.WriteLine("   * If you want to suppress console output add --Q as a");
				Console.WriteLine("     parameter.");
				Console.WriteLine();
				Console.WriteLine("   * the following GraphDS Service Plugins are initialized and started: ");

				foreach (var Service in _dsServer.AvailableServices)
				{
					Console.WriteLine("      * "+Service.PluginName);
				}
				Console.WriteLine();

				foreach (var Service in _dsServer.AvailableServices)
				{
					Console.WriteLine(Service.ServiceDescription);
					Console.WriteLine();
				}

				Console.WriteLine("Enter 'shutdown' to initiate the shutdown of this instance.");
			}

			Console.CancelKeyPress += OnCancelKeyPress;
			
			while (!shutdown)
			{
				String command = Console.ReadLine();
				
				if (!_ctrlCPressed)
				{
					if (command != null)
					{
						if (command.ToUpper() == "SHUTDOWN")
							shutdown = true;
					}
				}
			}

			Console.WriteLine("Shutting down GraphDS Server");
			_dsServer.Shutdown(null);
			Console.WriteLine("Shutdown complete");
			#endregion
		}

		#region
		/// <summary>
		///  Cancel KeyPress
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public virtual void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true; //do not abort Console here.
			_ctrlCPressed = true;
			Console.Write("Shutdown GraphDB (y/n)?");
			string input;
				do
				{
					input = Console.ReadLine();
				} while (input == null);

				switch (input.ToUpper())
				{
					case "Y":
						shutdown = true;
						return;
					default:
						shutdown = false;
						return;
				}
		}//method
		#endregion

	}
	#endregion

	public class sonesGraphDBStarter
	{
		static void Main(string[] args)
		{
			bool quiet = false;

			if (args.Count() > 0)
			{
				foreach (String parameter in args)
				{
					if (parameter.ToUpper() == "--Q")
						quiet = true;
				}
			}
			
			if (!quiet)
			{
				DiscordianDate ddate = new DiscordianDate();

				Console.WriteLine("sones GraphDB version 2.1-beta-2 - " + ddate.ToString());
				Console.WriteLine("(C) sones GmbH 2007-2011 - http://www.sones.com");
				Console.WriteLine("-----------------------------------------------");
				Console.WriteLine();
				Console.WriteLine("Starting up GraphDB...");
			}

			try
			{
				var sonesGraphDBStartup = new sonesGraphDBStartup(args);                
			}
			catch (ServiceException e)
			{
				if (!quiet)
				{ 
					Console.WriteLine(e.Message);
					Console.WriteLine("InnerException: " + e.InnerException.ToString());
					Console.WriteLine();
					Console.WriteLine("Press <return> to exit.");
					Console.ReadLine();
				}
			}
		}
	}
}
