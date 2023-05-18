using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using System.Threading;
using Npgsql;

namespace CustomerGrpc
{
	public class Program
	{
		public static int hilos;
		
		//Modificacion de parametros a recibir, creacion de hilos segun args[1]
		public static void Main(string[] args)
		{
			try
			{
				//Conexi�n con la base de datos
                TestConnection();
				hilos = Convert.ToInt32(args[1]);
				ThreadPool.SetMaxThreads(hilos,10);
				int maxThreads;
            	int comp1;
				ThreadPool.GetMaxThreads(out maxThreads, out comp1);
            	Console.WriteLine("The max number of threads: {0}", maxThreads);
				Console.WriteLine("The number of processors on this computer is {0}.", Environment.ProcessorCount);
                CreateHostBuilder(args).Build().Run();
            }
			catch (Exception)
			{
				Console.WriteLine("Puerto no valido / conexion errone. O debe agregar numero de hilos");
			}
		}

		public static int set_hilos_limit(){
			return hilos;
		}
        private static async void TestConnection()
        {
			//metodo utilizado para comprobar la conexi�n
            using (NpgsqlConnection con = GetConnection())
            {
				//momento en el que nos comectamos
                con.Open();
				if (con.State == ConnectionState.Open)
				{
					// HACER TRUNCATE
					Console.WriteLine("Conexi�n con la base de datos exitosa");
					using (var tx = con.BeginTransaction())
					{
						using (var cmd = new NpgsqlCommand())
						{
							cmd.Connection = con;
                            cmd.CommandText = "TRUNCATE public.\"calculos\"";
                            await cmd.ExecuteNonQueryAsync();
                        }
                        tx.Commit();
					}
				}
				else
				{
					Console.WriteLine("No se ha podido conectar a la base de datos");
				}
            }
        }

        private static NpgsqlConnection GetConnection()
        {
			//metodo de npgsql para conectarnos a la base de datos, retornamos la conexi�n
            return new NpgsqlConnection(@"Server=localhost;Port=5432;User Id=postgres;Password=admin;Database=proyectoSO");
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.ConfigureKestrel(options =>
					{
						// Setup a HTTP/2 endpoint without TLS for OSX.
						//ThreadPool.SetMaxThreads(1,0);
						options.ListenLocalhost(Int32.Parse(args[0]), o => o.Protocols = HttpProtocols.Http2);
					});
					webBuilder.UseStartup<Startup>();
				});
	}
}