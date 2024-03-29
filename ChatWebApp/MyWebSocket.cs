﻿using System;
using System.Text;
using Owin.WebSocket;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;

namespace ChatWebApp
{

    public class MyWebSocket : WebSocketConnection
    {
        string eventHubName = ConfigurationManager.AppSettings["eventHubName"];
        string chatRequestTopicPath = ConfigurationManager.AppSettings["chatRequestTopicPath"];
        string chatTopicPath = ConfigurationManager.AppSettings["chatTopicPath"];
        string connectionString = ConfigurationManager.AppSettings["eventHubConnectionString"];
        string serviceBusConnectionString = ConfigurationManager.AppSettings["serviceBusConnectionString"];

        string chatSessionSubscriptionName;
        string username;
        string sessionId;

        EventHubClient eventHubClient;
        SubscriptionClient chatSessionClient;
        NamespaceManager namespaceManager;

        void Init()
        {
            eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, eventHubName);
            namespaceManager = NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);
        }

        public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
        {
            //Handle the websocket message from the client
            var json = Encoding.UTF8.GetString(message.Array, message.Offset, message.Count);

            dynamic msg = JObject.Parse(json);

            if (msg.type.ToString() == "join")
            {

                //Join a group chat session
                sessionId = msg.sessionId;
                username = msg.username;
                JoinChatSession(sessionId);
                
            }
            else
            {
                string sessionId = msg.sessionId;
                string messageText = msg.message;
                SendChatMessage(messageText, sessionId);
            }  
        }

        private void JoinChatSession(string sessionId)
        {
            ConnectToChatSession(sessionId);

            SayHello(sessionId);
        }

        private void SayHello(string sessionId)
        {
            string automessage = string.Format("Hi, {0} is now in the chat.", username);
            SendChatMessage(automessage, sessionId);
        }

        private void ConnectToChatSession(string sessionId)
        {
            // Create new subscription that filters messages to that session ID
            chatSessionSubscriptionName = Guid.NewGuid().ToString();
            SqlFilter filter = new SqlFilter(string.Format("SessionId = '{0}'", sessionId));

            // Register a receiver on the subscription (filters by session ID)
            SubscriptionDescription subscriptionDescription = new SubscriptionDescription(chatTopicPath, chatSessionSubscriptionName);
            namespaceManager.CreateSubscription(subscriptionDescription, filter);
            chatSessionClient = SubscriptionClient.CreateFromConnectionString(serviceBusConnectionString, chatTopicPath, chatSessionSubscriptionName);
            chatSessionClient.OnMessage(ReceiveChatMessage);
        }

        void ReceiveChatMessage(BrokeredMessage message)
        {
            try
            {
                SendText(message.GetBody<byte[]>(), true);
                message.Complete();
            }
            catch (Exception ex)
            {
                //TODO: handle errors
            }

        }

        void DisconnectFromChatSession(string sessionId)
        {
            SayGoodBye(sessionId);

            //Delete the subscription and close the client
            namespaceManager.DeleteSubscription(chatTopicPath, chatSessionSubscriptionName);
            chatSessionClient.Close();
        }

         void SayGoodBye(string sessionId)
        {
            string automessage = string.Format("{0} has left the chat.", username);
            SendChatMessage(automessage, sessionId);
        }

        private void SendChatMessage(string chatText, string sessionId)
        {
            var message = new
            {
                message = chatText,
                createDate = DateTime.UtcNow,
                username = username,
                sessionId = sessionId,
                messageId = Guid.NewGuid().ToString()
            };

            try
            {
                // Use an Event Hub sender, message includes session ID as a Property
                string jsonMessage = JsonConvert.SerializeObject(message);
                EventData eventData = new EventData(Encoding.UTF8.GetBytes(jsonMessage));
                eventData.Properties.Add("SessionId", sessionId);
                eventHubClient.Send(eventData);
            }
            catch (Exception ex)
            {
                //TODO: Enable logging
                Console.WriteLine(ex.Message);
            }
        }

        public override void OnOpen(){
            Init();
        }

        public override void OnClose(WebSocketCloseStatus? closeStatus, string closeStatusDescription){
            DisconnectFromChatSession(sessionId);           
        }

    }
}