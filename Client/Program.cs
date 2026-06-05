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

var serverAddress = "192.168.1.2";
var serverPort = 5000;

while (true)
{
    var authUI = new AuthUI(serverAddress, serverPort);
    var currentUser = await authUI.RunAsync();

    if (currentUser == null)
    {
        // User chose Exit
        break;
    }

    // Transition immediately to chat mode
    await HandleChatAsync(serverAddress, serverPort, currentUser);
}

// Ensure the application exits gracefully
return;

async Task<User> HandleServerLoginOrRegisterAsync(string address, int port)
{
    var command = string.Empty;

    while (true)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Parse(address), port);
        using var stream = client.GetStream();

        Console.WriteLine("Enter command ('help' for all commands):");
        command = Console.ReadLine()?.Trim().ToLower() ?? string.Empty;

        // Connect to server per request
        if (command == "register")
        {
            Console.WriteLine("Registering a new account...");
            Console.WriteLine("Enter username:");
            var username = Console.ReadLine()?.Trim() ?? string.Empty;
            Console.WriteLine("Enter password:");
            var password = Console.ReadLine()?.Trim() ?? string.Empty;

            var newUser = new User
            {
                Login = username,
                Password = password
            };
            await NetworkHelper.SendStringAsync("register", stream);
            await NetworkHelper.SendStringAsync(username, stream);
            await NetworkHelper.SendObjectAsync(newUser, stream);
            

            var registerResult = await NetworkHelper.ReceiveObjectAsync<RegisterResult>(stream);
            if (registerResult != null && registerResult.IsRegistered)
            {
                Console.WriteLine("Registration successful! You can now log in.");
                continue;
            }
            else
            {
                Console.WriteLine($"Registration failed: {registerResult?.Message}");
            }
        }
        else if (command == "login")
        { 
            Console.WriteLine("Logging in...");
            Console.WriteLine("Enter username:");
            var username = Console.ReadLine()?.Trim() ?? string.Empty;
            Console.WriteLine("Enter password:");
            var password = Console.ReadLine()?.Trim() ?? string.Empty;

            var user = new User
            {
                Login = username,
                Password = password
            };
            await NetworkHelper.SendStringAsync("login", stream);
            await NetworkHelper.SendStringAsync(username, stream);
            await NetworkHelper.SendObjectAsync(user, stream);
            
            var authResult = await NetworkHelper.ReceiveObjectAsync<AuthResult>(stream);

            if (authResult != null && authResult.IsAuthenticated)
            {
                Console.WriteLine("Login successful! You can now access the chat.");
                return user;
            }
            else
            {
                Console.WriteLine($"Login failed: {authResult?.Message}");
            }
        }
        else if (command == "help")
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("register - Register a new account");
            Console.WriteLine("login - Log in to your account");
            Console.WriteLine("chat - Start chatting with other users");
        }
        else if (command == "chat")
        {
            Console.WriteLine("Use the new UI flow. This command is deprecated.");
            continue;
        }
        else
        {
            Console.WriteLine("Unknown command. Type 'help' for a list of commands.");
        }
    }
}

async Task HandleChatAsync(string address, int port, User user)
{
    var ui = new ChatUI(address, port, user.Login);
    await ui.RunAsync();
    Console.Clear();
}


