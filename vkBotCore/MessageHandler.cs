﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using VkBotCore.Subjects;
using VkBotCore.UI;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.RequestParams;
using Message = VkNet.Model.Message;

namespace VkBotCore
{
	public class MessageHandler
	{
		public VkCoreApiBase VkApi { get; set; }

		private Dictionary<BaseChat, Queue<long>> _lastMessages;
		private long _messageResendBlockTime = 10;

		public MessageHandler(VkCoreApiBase vkApi)
		{
			VkApi = vkApi;

			_lastMessages = new Dictionary<BaseChat, Queue<long>>();

			InitializePoolWorker();
		}

		public virtual void OnMessage(IUser sender, string message, BaseChat chat, Message messageData)
		{
			//защита от дублированных или задержанных сообщений
			if ((DateTime.UtcNow - messageData.Date.Value).TotalSeconds > _messageResendBlockTime) return;

			var msgId = messageData.ConversationMessageId.Value;
			if (!_lastMessages.ContainsKey(chat))
				_lastMessages.Add(chat, new Queue<long>());
			else
			{
				if (_lastMessages[chat].Contains(msgId)) return;
				_lastMessages[chat].Enqueue(msgId);
				if (_lastMessages[chat].Count > 10)
					_lastMessages[chat].Dequeue();
			}
			//защита от дублированных или задержанных сообщений


			//actions
			if (messageData.Action != null)
			{
				if (chat is Chat _chat)
				{
					if (messageData.Action.Type == MessageAction.ChatKickUser)
					{
						//if (messageData.Action.MemberId == -VkApi.GroupId) //нет события при кике бота.
						//{
						//	_chat.OnKick(sender);
						//	VkApi._chatsCache.Remove(_chat.PeerId, out _);
						//	_lastMessages.Remove(_chat);
						//}
						//else
							_chat.OnKickUser(VkApi.GetUser(messageData.Action.MemberId.Value), sender);
						return;
					}
					else if (messageData.Action.Type == MessageAction.ChatInviteUser)
					{
						if (messageData.Action.MemberId == -VkApi.GroupId)
							_chat.OnJoin(sender);
						else
							_chat.OnAddUser(VkApi.GetUser(messageData.Action.MemberId.Value), sender, false);
						return;
					}
					else if (messageData.Action.Type == MessageAction.ChatInviteUserByLink)
					{
						_chat.OnAddUser(VkApi.GetUser(messageData.Action.MemberId.Value), null, true);
						return;
					}
				}
			}


			if (sender is User user)
			{
				//buttons
				if (!string.IsNullOrEmpty(messageData.Payload))
				{
					try
					{
						var payload = JsonConvert.DeserializeObject<KeyboardButtonPayload>(messageData.Payload);
						if (payload.Button != null)
						{
							var s = payload.Button.Split(':');
							OnButtonClick(chat, user, message, s[0], s.Length == 1 ? "0" : s[1], messageData);
							return;
						}
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
					}
				}


				//commands
				if ((message.StartsWith("/") || message.StartsWith(".")) && message.Length != 1)
				{
					try
					{
						message = message.Replace("ё", "е");
						VkApi.Core.PluginManager.HandleCommand(user, chat, message, messageData);

						message = message.Substring(1);
						var args = message.Split(' ').ToList();
						string commandName = args.First();
						args.Remove(commandName);

						chat.OnCommand(user, commandName.ToLower(), args.ToArray(), messageData);
					}
					catch (Exception e)
					{
						chat.SendMessageAsync("Комманда задана неверно!");
						VkApi.Core.Log.Error(e.ToString());
					}
					return;
				}
			}

			//other
			if (!OnGetMessage(new GetMessageEventArgs(chat, sender, message, messageData))) return;
			chat.OnMessasge(sender, message, messageData);
		}

		public virtual void OnButtonClick(BaseChat chat, User user, string message, string keyboardId, string buttonId, Message messageData)
		{
			if (!OnButtonClick(new ButtonClickEventArgs(chat, user, message, keyboardId, buttonId, messageData))) return;
			chat.InvokeButton(user, keyboardId, buttonId);
		}

		public void SendMessage(string message, long peerId, Keyboard keyboard = null, bool disableMentions = false)
		{
			SendMessage(new MessagesSendParams
			{
				RandomId = GetRandomId(),
				PeerId = peerId,
				Message = message,
				Keyboard = keyboard?.GetKeyboard(),
				DisableMentions = disableMentions
			});
		}

		public async Task SendMessageAsync(string message, long peerId, Keyboard keyboard = null, bool disableMentions = false)
		{
			await SendMessageAsync(new MessagesSendParams
			{
				RandomId = GetRandomId(),
				PeerId = peerId,
				Message = message,
				Keyboard = keyboard?.GetKeyboard(),
				DisableMentions = disableMentions
			});
		}

		public void SendMessageWithPool(string message, long peerId, Keyboard keyboard = null, bool disableMentions = false)
		{
			SendMessageWithPool(new MessagesSendParams
			{
				RandomId = GetRandomId(),
				PeerId = peerId,
				Message = message,
				Keyboard = keyboard?.GetKeyboard(),
				DisableMentions = disableMentions
			});
		}

		public void SendMessage(MessagesSendParams message)
		{
			try
			{
				VkApi.Messages.Send(message);
			}
			catch
			{
				BaseChat baseChat;
				VkApi._chatsCache.Remove(message.PeerId.Value, out baseChat);
				if (baseChat is Chat chat) chat.OnKick(null);
			}
		}

		public async Task SendMessageAsync(MessagesSendParams message)
		{
			var send = VkApi.Messages.SendAsync(message);
			await send;
			if(send.Exception != null)
			{
				BaseChat baseChat;
				VkApi._chatsCache.Remove(message.PeerId.Value, out baseChat);
				if (baseChat is Chat chat) chat.OnKick(null);
			}
		}

		public void SendMessageWithPool(MessagesSendParams message)
		{
			_sendPool.Add(message);
			if (!_poolTimer.Enabled)
				_poolTimer.Start();
		}

		public void SendKeyboard(Keyboard keyboard, long peerId)
		{
			SendMessage(new MessagesSendParams
			{
				RandomId = GetRandomId(),
				PeerId = peerId,
				Message = keyboard.Message,
				Keyboard = keyboard.GetKeyboard()
			});
		}

		public async Task SendKeyboardAsync(Keyboard keyboard, long peerId)
		{
			await SendMessageAsync(new MessagesSendParams
			{
				RandomId = GetRandomId(),
				PeerId = peerId,
				Message = keyboard.Message,
				Keyboard = keyboard.GetKeyboard()
			});
		}

		public void SendKeyboardWithPool(Keyboard keyboard, long peerId)
		{
			SendMessageWithPool(new MessagesSendParams
			{
				RandomId = GetRandomId(),
				PeerId = peerId,
				Message = keyboard.Message,
				Keyboard = keyboard.GetKeyboard()
			});
		}

		public void SendSticker(MessagesSendStickerParams message)
		{
			VkApi.Messages.SendSticker(message);
		}

		public bool DeleteMessage(ulong id)
		{
			return VkApi.Messages.Delete(new ulong[] { id }, false, (ulong)VkApi.GroupId, true).First().Value;
		}

		private Timer _poolTimer;
		private List<MessagesSendParams> _sendPool;
		private void InitializePoolWorker()
		{
			_sendPool = new List<MessagesSendParams>();

			_poolTimer = new Timer(15);
			_poolTimer.AutoReset = false;
			_poolTimer.Elapsed += async (s, e) =>
			{
				try
				{
					if (_sendPool.Count == 0) return;
					var messages = _sendPool;
					_sendPool = new List<MessagesSendParams>();
					foreach (var message in messages)
						await SendMessageAsync(message);
				}
				catch { }
			};
		}

		public event EventHandler<GetMessageEventArgs> GetMessage;

		protected virtual bool OnGetMessage(GetMessageEventArgs e)
		{
			GetMessage?.Invoke(this, e);

			return !e.Cancel;
		}

		public event EventHandler<ButtonClickEventArgs> ButtonClick;

		protected virtual bool OnButtonClick(ButtonClickEventArgs e)
		{
			ButtonClick?.Invoke(this, e);

			return !e.Cancel;
		}

		private Random rnd = new Random();

		private int GetRandomId()
		{
			return rnd.Next();
		}
	}

	public class GetMessageEventArgs : GetMessageEventArgs<IUser>
	{
		public GetMessageEventArgs(BaseChat chat, IUser sender, string message, Message messageData) : base(chat, sender, message, messageData)
		{
		}
	}

	public class GetMessageEventArgs<TUser> : EventArgs where TUser : IUser
	{
		public bool Cancel { get; set; }

		public BaseChat Chat { get; set; }

		public TUser Sender { get; set; }

		public string Message { get; set; }

		public Message MessageData { get; set; }

		public GetMessageEventArgs(BaseChat chat, TUser sender, string message, Message messageData)
		{
			Chat = chat;
			Sender = sender;
			Message = message;
			MessageData = messageData;
		}
	}
}
