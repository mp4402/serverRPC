using System;
using System.Linq;
using System.Threading.Tasks;
using CustomerGrpc.Providers;
using Grpc.Core;
using Microsoft.AspNetCore.Localization;
using Npgsql;
using System.Data;

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
			//Modificar entrega del mensaje para retornar unicamente al cliente y que incluya el resultado de la operaci�n
			var chatRoom = _chatRoomProvider.GetChatRoomById(message.RoomId);
			foreach (var customer in chatRoom.CustomersInRoom)
			{
				if (message.CustomerName.Equals(customer.Name))
				{
					// select del valor. 
					await customer.Stream.WriteAsync(message);
					Console.WriteLine($"Sent message from {message.CustomerName} to {customer.Name}");
					break;
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