//*Серверна частина
//*
//* Клієнт
//*
//*-Може зареєструватися на сервері - відправляє ім'я користувача та пароль
//* - Може авторизуватися на сервері - відправляє ім'я користувача та пароль якщо його ще немє
//* - Може переглядати список користувачів на сервері
//* - Може відправляти повідомлення іншим користувачам - лише одному користувачу
//* - Може отримувати повідомлення від інших користувачів з сервера
//* 
//* - Клієнт підключається до сервера - передає запит, отримує відповідь, відключається
//*
//*Робить це кожні 3 секунди
//*  * 
//* Сервер
//* - Приймає підключення від клієнтів
//* - Зберігає інформацію про користувачів та їх паролі
//* - Зберігає інформацію про повідомлення між користувачами
//* - Віддає користувачу список користувачів на сервері
//* - Віддає користувачу повідомлення від інших користувачів
//* - Приймає повідомлення від користувача та зберігає його для інших користувачів

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

Console.WriteLine("Server");
var tcpListener = new TcpListener(IPAddress.Any, 5000);
tcpListener.Start();

var userFilePath = "users.json";
var chatMessagesFilePath = "chat_messages.json";
var multicastAddress = "239.0.0.1";
var multicastPort = 5003;
var broadcastPort = 5111;
var serverAddress = "192.168.1.2";
var serverPort = 5000;

_ = Task.Run(() => ChatEventHandlerServer.HandleGetServerAddressAsync(serverAddress, serverPort, broadcastPort));
while (true)
{
    var client = await tcpListener.AcceptTcpClientAsync();

    _ = Task.Run(() => HandleClientAsync(client));
}

async Task HandleClientAsync(TcpClient client)
{
    Console.WriteLine("Client connected.");
    using var stream = client.GetStream();
    var command = await NetworkHelper.ReceiveStringAsync(stream);
    Console.WriteLine($"Received command: {command}");
    var currentUserLogin = await NetworkHelper.ReceiveStringAsync(stream);

    switch (command)
    {
        case "register":
            await ChatEventHandlerServer.HandleRegisterAsync(stream, userFilePath);
            break;
        case "login":
            await ChatEventHandlerServer.HandleLoginAsync(stream, userFilePath);
            break;
        case "get_users":
            await ChatEventHandlerServer.HandleGetUsersAsync(stream, currentUserLogin, userFilePath);
            break;
        case "chat_history":
            var targetLogin = await NetworkHelper.ReceiveStringAsync(stream);
            var chatHistory = await ChatEventHandlerServer.HandleGetChatHistoryAsync(stream, currentUserLogin, targetLogin, chatMessagesFilePath);
            await NetworkHelper.SendObjectAsync(chatHistory, stream);
            break;
        case "send_message":
            var message = await NetworkHelper.ReceiveObjectAsync<Message>(stream);
            await ChatEventHandlerServer.HandleSendMessage(stream, message, chatMessagesFilePath, multicastAddress, multicastPort);
            break;
        case "help":
        Console.WriteLine("Available commands:");
        Console.WriteLine("register - Register a new account");
        Console.WriteLine("login - Log in to your account");
        Console.WriteLine("get_users - Get a list of all users");
        Console.WriteLine("chat_history - Get chat history with another user");
        Console.WriteLine("send_message - Send a message to another user");
        break;
        case "get_new_messages":
            await ChatEventHandlerServer.HandleGetNewMessagesAsync(stream, currentUserLogin, chatMessagesFilePath);
            break;
        default:
            Console.WriteLine($"Unknown command: {command}");
            break;
    }
    client.Close();
}














