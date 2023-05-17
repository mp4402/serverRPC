using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Npgsql;

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
			Program.TestConnection();
			return new JoinCustomerReply { RoomId = await _chatRoomService.AddCustomerToChatRoomAsync(request.Customer) };
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
							_logger.LogInformation("Prueba de conexion!");
							var con = new NpgsqlConnection(connectionString: @"Server=localhost;Port=5432;User Id=postgres;Password=;Database=proyectoSO");
							con.Open();
							if (con.State == ConnectionState.Open)
							{
								Console.WriteLine("Conexi�n con la base de datos exitosa, desde sendmessage");
							}
							else
							{
								Console.WriteLine("No se ha podido conectar a la base de datos, desde sendmessage");
							}
                            _chatRoomService.DisconnectCustomer(requestStream.Current.RoomId, Guid.Parse(requestStream.Current.CustomerId));
							break;
                        }
                        //posible invocaci�n de funciones de c�lculo estad�stico y lectura/escritura de la base de datos aqu�
                        //�rea cr�tica
                        await _chatRoomService.BroadcastMessageAsync(requestStream.Current);
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