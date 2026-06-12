using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class ChatEventHandlerServer
{

    private static readonly UdpClient _udpClient = new UdpClient();
    private static object _fileLock = new object();
    public static async Task<bool> HandleLoginAsync(NetworkStream stream, string userFilePath)
    {
        var user = await NetworkHelper.ReceiveObjectAsync<User>(stream);
        if (user == null)
        {
            Console.WriteLine("Failed to receive user information.");
            return false;
        }
        var loginResult = await User.AuthenticateUserAsync(user, userFilePath);
        await NetworkHelper.SendObjectAsync(loginResult, stream);
        return loginResult.IsAuthenticated;
    }

    public static async Task<bool> HandleRegisterAsync(NetworkStream stream, string userFilePath)
    {
        var newUser = await NetworkHelper.ReceiveObjectAsync<User>(stream);
        if (newUser == null) { return false; }
        var registerResult = await User.RegisterUserAsync(newUser, userFilePath);
        await NetworkHelper.SendObjectAsync(registerResult, stream);
        return registerResult.IsRegistered;
    }

    public static async Task<List<string>> HandleGetUsersAsync(NetworkStream stream, string currentUserLogin, string userFilePath)
    {
        Console.WriteLine("Client requested user list.");
        //sending all users to client
        var userList = JsonSerializer.Deserialize<List<User>>(await File.ReadAllTextAsync(userFilePath));
        if (userList == null) { return new List<string>(); }
        var userListLogins = userList.Select(u => u.Login).Where(u => !string.IsNullOrEmpty(u) && u != currentUserLogin).ToList();
        await NetworkHelper.SendObjectAsync(userListLogins, stream);
        return userListLogins;
    }

    public static async Task<List<Message>> HandleGetChatHistoryAsync(
        NetworkStream stream,
        string currentUserLogin,
        string targetLogin,
        string chatMessagesFilePath)
    {
        if (!File.Exists(chatMessagesFilePath))
        {
            await File.WriteAllTextAsync(
                chatMessagesFilePath,
                "[]");
        }
        var messages = JsonSerializer.Deserialize<List<Message>>(await File.ReadAllTextAsync(chatMessagesFilePath));
        if (messages == null) { return new List<Message>(); }
        var filteredMessages =
        messages.Where(m =>

            (m.Sender == currentUserLogin &&
             m.Recipient == targetLogin)

             ||

            (m.Sender == targetLogin &&
             m.Recipient == currentUserLogin)

        )
        .OrderBy(m => m.SendingDate)
        .ToList();
        return filteredMessages;
    }
    public static async Task HandleSendMessage(NetworkStream stream, Message? message, string chatMessagesFilePath, string multicastAddress, int multicastPort)
    {
        if (!File.Exists(chatMessagesFilePath))
        {
            await File.WriteAllTextAsync(
                chatMessagesFilePath,
                "[]");
        }
        lock (_fileLock)
        {
            var messages = JsonSerializer.Deserialize<List<Message>>(File.ReadAllText(chatMessagesFilePath)) ?? new List<Message>();
            if (messages == null || message == null) { return; }
            messages.Add(message);
            File.WriteAllText(chatMessagesFilePath, JsonSerializer.Serialize(messages));
        }

        // send udp notification about new message
        try
        {
            var notification = new MessageNotification
            {
                Sender = message.Sender,
                Recipient = message.Recipient,
            };

            var jsonNotification = JsonSerializer.Serialize(notification);
            byte[] data = Encoding.UTF8.GetBytes(jsonNotification);
            await _udpClient.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(multicastAddress), multicastPort));
        }
        catch (SocketException)
        {
            Console.Clear();
            Console.WriteLine("Error sending UDP notification: Socket error");
        }
        catch (Exception ex)
        {
            Console.Clear();
            Console.WriteLine($"Error sending UDP notification: {ex.Message}");
        }

    }

    public static async Task HandleGetNewMessagesAsync(
        NetworkStream stream,
        string login,
        string chatMessagesFilePath
        )
    {
        if (!File.Exists(chatMessagesFilePath))
        {
            Console.WriteLine("Chat messages file not found. Creating a new one.");
            await File.WriteAllTextAsync(
                chatMessagesFilePath,
                "[]");
        }

        var messages = new List<Message>();
        var json = string.Empty;

        lock (_fileLock)
        {
            json = File.ReadAllText(chatMessagesFilePath);
            messages = JsonSerializer.Deserialize<List<Message>>(json)
            ?? new List<Message>();
        }

        var newMessages =
        messages
        .Where(m =>
            m.Recipient == login &&
            !m.IsDelivered)
        .ToList();

        foreach (var msg in newMessages)
        {
            msg.IsDelivered = true;
        }

        await File.WriteAllTextAsync(
            chatMessagesFilePath,
            JsonSerializer.Serialize(
                messages,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        await NetworkHelper.SendObjectAsync(
            newMessages,
            stream);
    }
}




public class ChatEventHandlerClient
{
   public static async Task<List<string>?> GetUsersAsync(string address, int port, string login)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(address), port);

        using var stream = client.GetStream();

        await NetworkHelper.SendStringAsync("get_users", stream);
        await NetworkHelper.SendStringAsync(login, stream);

        return await NetworkHelper.ReceiveObjectAsync<List<string>>(stream);
    }

    public static async Task<List<Message>?> GetChatHistoryAsync(
    string address,
    int port,
    string login,
    string targetUser)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(address), port);

        using var stream = client.GetStream();

        await NetworkHelper.SendStringAsync("chat_history", stream);
        await NetworkHelper.SendStringAsync(login, stream);
        await NetworkHelper.SendStringAsync(targetUser, stream);

        return await NetworkHelper.ReceiveObjectAsync<List<Message>>(stream);
    }

    public static async Task SendMessageAsync(
    string address,
    int port,
    string login,
    string recipient,
    string text)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(address), port);

        using var stream = client.GetStream();

        await NetworkHelper.SendStringAsync("send_message", stream);
        await NetworkHelper.SendStringAsync(login, stream);

        var message = new Message
        {
            Sender = login,
            Recipient = recipient,
            Text = text,
            SendingDate = DateTime.Now
        };

        await NetworkHelper.SendObjectAsync(message, stream);
    }

        //Доробити проєкт з TCP чатом
        //Замінити поллінг(кожні 3 секунди) на multicast повідомлення
        //про нові повідомлення в чаті

        //Щоб не усі клієни постійно опитували сервер, а отримували повідомлення про нові повідомлення в чаті через multicast,
        //і тоді підключалися до сервера для отримання нових повідомлень.

        //в повідомленні multicast передавати інформацію про те,
        //що є нове повідомлення в чаті, та від кого та кому
        //і лише ті клієни яких це стосується отримують повідомлення через TCP, а не всі клієнти
        //підключаються до сервера для отримання нових повідомлень.



    //public static async Task NewMessagesRequest(string address, int port, string multicastAddress, int multicastPort, string login)
    //{
    //    try
    //    {
    //        var udpClient = new UdpClient(multicastPort);
    //        udpClient.JoinMulticastGroup(IPAddress.Parse(multicastAddress));
    //        Console.WriteLine("Waiting multicast...");
    //        await udpClient.SendAsync(Encoding.UTF8.GetBytes(login), Encoding.UTF8.GetByteCount(login), new IPEndPoint(IPAddress.Parse(multicastAddress), multicastPort));
    //        var data = await udpClient.ReceiveAsync();
    //        Console.WriteLine($"Received: {Encoding.UTF8.GetString(data.Buffer)}");

    //        if (Encoding.UTF8.GetString(data.Buffer).Contains(login))
    //        {
    //            Console.WriteLine("New messages for you! Connecting to server...");
    //            await GetNewMessagesTCP(address, port, login);
    //        }
    //    }
    //    catch 
    //    {
    //        //Console.WriteLine(ex.ToString());
    //    }
       

    //}

}
