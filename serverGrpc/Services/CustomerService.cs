using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Localization;
using Npgsql;
using System.Data;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace CustomerGrpc.Services
{
	public class CustomerService : CustomerGrpc.CustomerService.CustomerServiceBase
	{

		private readonly ILogger<CustomerService> _logger;
		private readonly IChatRoomService _chatRoomService;


		public CustomerService(ILogger<CustomerService> logger, IChatRoomService chatRoomService)
		{
			_logger = logger;
			_chatRoomService = chatRoomService;
		}

		public override async Task<JoinCustomerReply> JoinCustomerChat(JoinCustomerRequest request, ServerCallContext context)
		{
			return new JoinCustomerReply { RoomId = await _chatRoomService.AddCustomerToChatRoomAsync(request.Customer) };
		}
		 static double readFile(string path, string function){
            List<int> Open = new List<int>();
            using(var reader = new StreamReader(@path))
            {
                int countLines = 0;
                while (!reader.EndOfStream)
                {
                    countLines = countLines + 1; 
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    if (countLines > 1){
                        int openValue = Convert.ToInt32(values[1]); // solo este interesa\
                        Open.Add(openValue);
                    }
                }
            }
			double avgOpen = Math.Round(Open.Average(),2);
			double respuesta_file = 0;
            switch(function){
				case "std":
						respuesta_file =  Math.Round(Math.Sqrt(Open.Average(v=>Math.Pow(v-avgOpen,2))),2);

					break;
				case "min":
						respuesta_file = Open.Min();
					break;
				case "max":
						respuesta_file = Open.Max();
					break;
				case "mean":
						respuesta_file = avgOpen;
					break;
				case "count":
						respuesta_file = Open.Count();
					break;
				default:
					break;
			}
			return respuesta_file;
            
        }
        public async void EjecutarTarea(Object valor)
        {
			ChatMessage message = (ChatMessage)valor;
			try{
				Console.WriteLine("ES por la variable message: " + message.Message);
			}
			catch(Exception a){
				Console.WriteLine("SI ES EL MESSAGE" + message.Message);
				Console.WriteLine(a);
			}
			using (var con = new NpgsqlConnection(connectionString: @"Server=localhost;Port=5432;User Id=postgres;Password=;Database=proyectoSO"))
			{
				con.Open();
				if (con.State == ConnectionState.Open)
				{
					Console.WriteLine("Conexi�n con la base de datos exitosa, desde sendmessage");
					//posible invocaci�n de funciones de c�lculo estad�stico y lectura/escritura de la base de datos aqu�
					string path = Environment.CurrentDirectory + @"/so_data/index_data_" + message.Message + ".csv";
					double result_hilo = readFile(path, message.FunctionProcess);
					//�rea cr�tica
					using(var tx = con.BeginTransaction())
					{
						using(var cmd = new NpgsqlCommand())
						{	//.. do som database stuff ..
							cmd.Connection = con;
							cmd.CommandText = $"SELECT * FROM public.calculos WHERE file='{message.Message}'";
							NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
							var result = new List<double>();
							Boolean bandera = false; // No existe la fila entera, se hace insert. 
							while (await reader.ReadAsync())
							{
								try {
									result.Add((double)reader[0]);
									result.Add((double)reader[1]);
									result.Add((double)reader[2]);
									result.Add((double)reader[3]);
									result.Add((double)reader[4]);
									result.Add((double)reader[5]);
									for(int i=0;i<result.Count;i++)
									{
									Console.WriteLine(result[i]);
									}
									bandera = true; // si existe la fila entera
									break;
								}
								catch(Exception){
									Console.WriteLine("No hay datos con ese file de id. ");
									bandera = false; // No existe la fila entera, se hace insert. 
									break;
								}
							}
							if (bandera){
								cmd.CommandText = $"UPDATE public.\"calculos\"" +
								$" 	SET \"functionProcessed\"=\"functionProcessed\" + 1, \"'{message.FunctionProcess}'\"='{result_hilo}'" +
								$" WHERE file = {message.Message}";
								await cmd.ExecuteNonQueryAsync();
							}
							else{
								// insert 
								cmd.CommandText = $"INSERT INTO public.calculos(file, \"'{message.FunctionProcess}'\", \"functionProcessed\") VALUES (@file, @function, @functionProcessed);";
								cmd.Parameters.AddWithValue("@file", message.Message);
								cmd.Parameters.AddWithValue("@function", result_hilo);
								cmd.Parameters.AddWithValue("@functionProcessed", 1);
								await cmd.ExecuteNonQueryAsync();
							}
						}
						tx.Commit();
					}
					// fin área crítica
					// cambiar el message respuesta. 
					message.Respuesta = result_hilo.ToString();
					await _chatRoomService.BroadcastMessageAsync(message);
				}
				else
				{
					Console.WriteLine("No se ha podido conectar a la base de datos, desde sendmessage");
				}
			}
        }

        public override async Task SendMessageToChatRoom(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream,
			ServerCallContext context)
		{
			var httpContext = context.GetHttpContext();
			_logger.LogInformation($"Connection id: {httpContext.Connection.Id}");

			if (!await requestStream.MoveNext())
			{
				return;
			}

			_chatRoomService.ConnectCustomerToChatRoom(requestStream.Current.RoomId, Guid.Parse(requestStream.Current.CustomerId), responseStream);
			var user = requestStream.Current.CustomerName;
			_logger.LogInformation($"{user} connected");

			try
			{
				while (await requestStream.MoveNext())
				{
					if (!string.IsNullOrEmpty(requestStream.Current.Message))
					{
						if (string.Equals(requestStream.Current.Message.ToLower(), "quit", StringComparison.OrdinalIgnoreCase))
						{
                            _chatRoomService.DisconnectCustomer(requestStream.Current.RoomId, Guid.Parse(requestStream.Current.CustomerId));
							break;
                        }
						
                        
						// si no es quit, utilizar el poolthread. 
						// esa funcion debe de: 
							// leer el archivo, y calcular lo solicitado. 
							// 
						//await _chatRoomService.BroadcastMessageAsync(requestStream.Current); // esta se hace en el thread. 
						ThreadPool.QueueUserWorkItem(EjecutarTarea, requestStream.Current);
					}
				}
			}
			catch (IOException)
			{
				_chatRoomService.DisconnectCustomer(requestStream.Current.RoomId, Guid.Parse(requestStream.Current.CustomerId));
				_logger.LogInformation($"Connection for {user} was aborted.");
			}
		}
	}
}