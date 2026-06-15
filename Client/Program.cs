//Клієнтська частина

//При завантаженні клієнтського додатку користувач може 
//вибрати між реєстрацією та авторизацією. 
//Якщо користувач вибирає реєстрацію, він вводить своє ім'я користувача та пароль, 
//які відправляються на сервер для створення нового облікового запису. 
//Якщо користувач вибирає авторизацію, він вводить свої облікові дані,
//які перевіряються на сервері для підтвердження його особи.

//Якщо успішна авторизація або реєстрація
//клієнт може почати отримувати список користувачів, які підключені до сервера,
//та отримувати повідомлення від інших користувачів.

//якщо він сам хоче відправити повідомлення іншому користувачу,
//він вибирає його зі списку та вводить текст повідомлення,
//і воно відправляється на сервер, який зберігає його та доставляє отримувачу при наступному запиті.


using System;
using System.Net;
using System.Net.Sockets;

Console.WriteLine("Client");

var udpClient = new UdpClient();

//var serverAddress = "192.168.1.2";
//var serverPort = 5000;
var broadcastAddress = IPAddress.Broadcast;
var broadcastPort = 5111;

Console.WriteLine("Searching for server...");

ServerConfig? serverConfig;

serverConfig = await ChatEventHandlerClient.GetServerInfoAsync(broadcastPort);
if (serverConfig == null)
{
    Console.WriteLine("Could not find server on local network. Press any key to exit.");
    Console.ReadKey();
    return;
}
Console.WriteLine($"Connected to server {serverConfig.Address}:{serverConfig.Port}");


while (true)
{
    
    var authUI = new AuthUI(serverConfig.Address, serverConfig.Port);
    var currentUser = await authUI.RunAsync();
    

    if (currentUser == null)
    {
        // User chose Exit
        break;
    }

    await HandleChatAsync(serverConfig.Address, serverConfig.Port, currentUser);
}

return;

async Task HandleChatAsync(string address, int port, User user)
{
    var ui = new ChatUI(address, port, user.Login);
    await ui.RunAsync();
    Console.Clear();
}


