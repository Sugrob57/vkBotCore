﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VkBotCore.UI;
using Message = VkNet.Model.Message;

namespace VkBotCore.Subjects
{
	/// <summary>
	/// Базовый класс для взаимодействия с диалогом и пользователями в нём.
	/// </summary>
	public abstract class BaseChat : IEquatable<BaseChat>
	{
		/// <summary>
		/// VkApi обработчик управляющего сообщества.
		/// </summary>
		public VkCoreApiBase VkApi { get; set; }

		/// <summary>
		/// Идентификатор диалога.
		/// </summary>
		public long PeerId { get; set; }

		/// <summary>
		/// Определяет, является ли данный диалог личной перепиской пользователя с сообществом.
		/// </summary>
		public bool IsConversation { get => IsUserConversation(PeerId); }

		/// <summary>
		/// Определяет, является ли данный диалог личной перепиской пользователя с сообществом.
		/// </summary>
		public static bool IsUserConversation(long peerId) => peerId < 2000000000;


		private Dictionary<string, Keyboard> _cachedKeyboards;

		public Keyboard BaseKeyboard { get; set; }

		public BaseChat(VkCoreApiBase vkApi, long peerId)
		{
			VkApi = vkApi;
			PeerId = peerId;
			_cachedKeyboards = new Dictionary<string, Keyboard>();
		}

		protected internal virtual void OnMessasge(IUser sender, string message, Message messageData)
		{

		}

		protected internal virtual void OnCommand(User sender, string command, string[] args, Message messageData)
		{

		}

		/// <summary>
		/// Отправляет текстовое сообщение в диалог.
		/// </summary>
		public virtual void SendMessage(string message, bool disableMentions = false)
		{
			if (!string.IsNullOrEmpty(message))
				VkApi.MessageHandler.SendMessage(message, PeerId, BaseKeyboard, disableMentions);
		}

		/// <summary>
		/// Отправляет текстовое сообщение в диалог. (Асинхронная отправка)
		/// </summary>
		public virtual async Task SendMessageAsync(string message, bool disableMentions = false)
		{
			await VkApi.MessageHandler.SendMessageAsync(message, PeerId, BaseKeyboard, disableMentions);
		}

		/// <summary>
		/// Отправляет текстовое сообщение в диалог. (Упорядоченная отправка через пул)
		/// </summary>
		public virtual void SendMessageWithPool(string message, bool disableMentions = false)
		{
			VkApi.MessageHandler.SendMessageWithPool(message, PeerId, BaseKeyboard, disableMentions);
		}

		/// <summary>
		/// Отправляет текстовое сообщение в диалог.
		/// </summary>
		public void SendMessage(object obj, bool disableMentions = false)
		{
			SendMessage(obj?.ToString(), disableMentions);
		}

		/// <summary>
		/// Отправляет текстовое сообщение в диалог. (Асинхронная отправка)
		/// </summary>
		public async Task SendMessageAsync(object obj, bool disableMentions = false)
		{
			await SendMessageAsync(obj?.ToString(), disableMentions);
		}

		/// <summary>
		/// Отправляет текстовое сообщение в диалог. (Упорядоченная отправка через пул)
		/// </summary>
		public virtual void SendMessageWithPool(object obj, bool disableMentions = false)
		{
			SendMessageWithPool(obj?.ToString(), disableMentions);
		}

		/// <summary>
		/// Отправляет клавиатуру в диалог по её идентификатору.
		/// </summary>
		public void SendKeyboard(string keyboardId)
		{
			if (_cachedKeyboards.ContainsKey(keyboardId))
				SendKeyboard(_cachedKeyboards[keyboardId]);
		}

		/// <summary>
		/// Отправляет клавиатуру в диалог по её идентификатору. (Асинхронная отправка)
		/// </summary>
		public async Task SendKeyboardAsync(string keyboardId)
		{
			if (_cachedKeyboards.ContainsKey(keyboardId))
				await SendKeyboardAsync(_cachedKeyboards[keyboardId]);
		}

		/// <summary>
		/// Отправляет клавиатуру в диалог по её идентификатору.
		/// </summary>
		public void SendKeyboardWithPool(string keyboardId)
		{
			if (_cachedKeyboards.ContainsKey(keyboardId))
				SendKeyboardWithPool(_cachedKeyboards[keyboardId]);
		}

		/// <summary>
		/// Отправляет клавиатуру в диалог.
		/// </summary>
		public void SendKeyboard(Keyboard keyboard)
		{
			AddKeyboard(keyboard);
			VkApi.MessageHandler.SendKeyboard(keyboard, PeerId);
		}

		/// <summary>
		/// Отправляет клавиатуру в диалог. (Асинхронная отправка)
		/// </summary>
		public async Task SendKeyboardAsync(Keyboard keyboard)
		{
			AddKeyboard(keyboard);
			await VkApi.MessageHandler.SendKeyboardAsync(keyboard, PeerId);
		}

		/// <summary>
		/// Отправляет клавиатуру в диалог. (Упорядоченная отправка через пул)
		/// </summary>
		public void SendKeyboardWithPool(Keyboard keyboard)
		{
			AddKeyboard(keyboard);
			VkApi.MessageHandler.SendKeyboardWithPool(keyboard, PeerId);
		}

		/// <summary>
		/// Добавляет клавиатуру в кэш для дальнейшего вызова по идентификатору.
		/// </summary>
		public void AddKeyboard(Keyboard keyboard)
		{
			if (!_cachedKeyboards.ContainsKey(keyboard.Id))
				_cachedKeyboards.Add(keyboard.Id, keyboard);
			else
				_cachedKeyboards[keyboard.Id] = keyboard;
		}

		public void InvokeButton(User user, string keyboardId, string buttonId)
		{
			if (BaseKeyboard?.Id == keyboardId)
			{
				BaseKeyboard.TryInvokeButton(this, user, buttonId);
				return;
			}
			if (_cachedKeyboards.ContainsKey(keyboardId))
			{
				var keyboard = _cachedKeyboards[keyboardId];

				if (keyboard.OneTime)
					_cachedKeyboards.Remove(keyboardId);

				keyboard.TryInvokeButton(this, user, buttonId);
			}
		}

		public bool RemoveMessage(ulong id)
		{
			return VkApi.MessageHandler.DeleteMessage(id);
		}

		public override bool Equals(object obj) => obj is BaseChat chat && Equals(chat);
		public bool Equals(BaseChat other) => other.PeerId == PeerId && other.VkApi.Equals(VkApi);

		public override int GetHashCode() => (int)PeerId;
	}

	public class ChatEventArgs : EventArgs
	{
		public BaseChat Chat { get; }

		public ChatEventArgs(BaseChat chat)
		{
			Chat = chat;
		}
	}
}