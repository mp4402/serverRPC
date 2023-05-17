using System;
using System.Linq;
using System.Threading.Tasks;
using CustomerGrpc.Providers;
using Grpc.Core;
using Microsoft.AspNetCore.Localization;

namespace CustomerGrpc.Services
{
	public class ChatRoomService : IChatRoomService
	{
		private readonly IChatRoomProvider _chatRoomProvider;
		
		public ChatRoomService(IChatRoomProvider chatRoomProvider)
		{
			_chatRoomProvider = chatRoomProvider;
		}
		
		public async Task BroadcastMessageAsync(ChatMessage message)
		{
            //Modificar entrega del mensaje para retornar unicamente al cliente y que incluya el resultado de la operación
            var banderaEscrito = false;
			var chatRoom = _chatRoomProvider.GetChatRoomById(message.RoomId);
			//ya no hay que escribir al otro cliente, unicamente al que hace el request
			//posible invocación de funciones de cálculo estadístico y lectura/escritura de la base de datos aquí
			//área crítica
			foreach (var customer in chatRoom.CustomersInRoom)
			{
				if (message.CustomerDest.Equals(customer.Name))
				{
					await customer.Stream.WriteAsync(message);
                    Console.WriteLine($"Sent message from {message.CustomerName} to {customer.Name}");
					banderaEscrito = true;
					break;
                }
            } 
			if (banderaEscrito)
			{
                foreach (var customer in chatRoom.CustomersInRoom)
				{
					if (message.CustomerName.Equals(customer.Name))
					{
						await customer.Stream.WriteAsync(message);
						Console.WriteLine($"Sent message from {message.CustomerName} to {customer.Name}");
						break;
					}
				}

            }
			else 
			{
                foreach (var customer in chatRoom.CustomersInRoom)
                {
                    if (message.CustomerName.Equals(customer.Name))
                    {
						message.Message = "No existe el usuario " + message.CustomerDest;
                        await customer.Stream.WriteAsync(message);
                        Console.WriteLine($"Sent invalid message");
                        break;
                    }
                }
            }
        }

		public Task<int> AddCustomerToChatRoomAsync(Customer customer)
		{
			bool bandera = true;
			var room = _chatRoomProvider.GetFreeChatRoom();
			foreach (var cust in room.CustomersInRoom)
			{

				if (cust.Name.Equals(customer.Name))
				{
					bandera = false;
					break;
				}
			}
			if (bandera)
			{
                room.CustomersInRoom.Add(new Models.Customer
                {
                    ColorInConsole = customer.ColorInConsole,
                    Name = customer.Name,
                    Id = Guid.Parse(customer.Id)
                });
                return Task.FromResult(room.Id);
            }
			return Task.FromResult(-1);
		}

		public void ConnectCustomerToChatRoom(int roomId, Guid customerId, IAsyncStreamWriter<ChatMessage> responseStream)
		{
			_chatRoomProvider.GetChatRoomById(roomId).CustomersInRoom.FirstOrDefault(c => c.Id == customerId).Stream = responseStream;
		}
		
		public void DisconnectCustomer(int roomId, Guid customerId)
		{
			var room = _chatRoomProvider.GetChatRoomById(roomId);
			room.CustomersInRoom.Remove(room.CustomersInRoom.FirstOrDefault(c => c.Id == customerId));
		}
	}
}