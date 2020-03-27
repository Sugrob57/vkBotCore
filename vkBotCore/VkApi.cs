﻿using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using VkBotCore.Configuration;
using VkNet;
using VkNet.Model;

namespace VkBotCore
{
    public class VkCoreApi : VkCoreApiBase
    {
        Dictionary<long, VkCoreApiBase> _vkApi { get; set; }

        public VkCoreApi(BotCore core) : base(core, 0)
        {
            _vkApi = new Dictionary<long, VkCoreApiBase>();

            var accesToken = Core.Configuration.GetValue<string>($"Config:AccessToken", null);
            if (accesToken != null)
                Authorize(new ApiAuthParams { AccessToken = accesToken });

            LoadAll();
        }

        public void LoadAll()
        {
            foreach (var a in Core.Configuration.GetSection("Config:Groups").GetChildren())
                Get(long.Parse(a.Key));
        }

        public VkCoreApiBase Get(long groupId)
        {
            if (_vkApi.ContainsKey(groupId))
                return _vkApi[groupId];
            var api = new VkCoreApiBase(Core, groupId);
            var accesToken = Core.Configuration.GetValue<string>($"Config:Groups:{groupId}:AccessToken", null);
            if (accesToken == null)
                return this;
            api.Authorize(new ApiAuthParams { AccessToken = accesToken });
            _vkApi.Add(groupId, api);
            return api;
        }

        public VkCoreApiBase[] GetAvailableApis(string _namespace)
        {
            var apis = _vkApi.Values.Where(a => a.AvailableNamespaces.Contains(_namespace));
            if(AvailableNamespaces.Contains(_namespace))
            {
                var _apis = apis.ToList();
                _apis.Add(this);
                return _apis.ToArray();
            }
            return apis.ToArray();
        }
    }

    public class VkCoreApiBase : VkApi
    {
        public BotCore Core { get; private set; }

        /// <summary>
        /// Идентификатор сообщества.
        /// </summary>
        public long GroupId { get; private set; }

        private Dictionary<long, Chat> Chats { get; set; }

        /// <summary>
        /// Обработчик сообщений.
        /// </summary>
        public MessageHandler MessageHandler { get; private set; }


        /// <summary>
        /// Обрабатываемые пространства имён.
        /// </summary>
        public string[] AvailableNamespaces { get; private set; }

        internal Storages StoragesCache { get; set; }

        public VkCoreApiBase(BotCore core, long groupId)
        {
            Core = core;
            GroupId = groupId;
            Chats = new Dictionary<long, Chat>();
            MessageHandler = new MessageHandler(this);
            StoragesCache = new Storages();
            AvailableNamespaces = Core.Configuration.GetArray($"Config:Groups:{groupId}:AvailableNamespaces", new string[0]);
        }

        /// <summary>
        /// Перопределение создания чатов.
        /// </summary>
        public Func<VkCoreApiBase, long, Chat> GetNewChat { get; set; }

        public Chat GetChat(long peerId)
        {
            if (!Chats.ContainsKey(peerId))
            {
                Chat chat = GetNewChat?.Invoke(this, peerId) ?? new Chat(this, peerId);
                Chats.Add(peerId, chat);

                Core.VkApi.OnChatCreated(new ChatEventArgs(chat));
                if (Core.VkApi != this)
                    OnChatCreated(new ChatEventArgs(chat));
                return chat;
            }
            else
                return Chats[peerId];
        }

        /// <summary>
        /// Событие вызываемое при инициализации чата.
        /// </summary>
        public event EventHandler<ChatEventArgs> ChatCreated;

        protected virtual void OnChatCreated(ChatEventArgs e)
        {
            ChatCreated?.Invoke(this, e);
        }
    }
}
