using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public abstract class NetworkHelper
{
    

    public static async Task SendStringAsync(string text, NetworkStream stream)
    {
        // перетворюємо рядок у байти
        byte[] messageBytes = Encoding.UTF8.GetBytes(text);
        // визначемо його розмір
        int messageLength = messageBytes.Length;

        // передамо розмір
        byte[] lengthBytes = BitConverter.GetBytes(messageLength);
        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);

        // передамо байти
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);

        // "hello" -> [5][h][e][l][l][o][4][p][i][n][g][5][r][a][n][d][o][m]


    }

    public static async Task<string> ReceiveStringAsync(NetworkStream stream)
    {
        byte[] lengthBytes = new byte[sizeof(int)];

        // спочатку зчитуємо розмір повідомлення
        await stream.ReadAsync(lengthBytes, 0, lengthBytes.Length);
        int messageLength = BitConverter.ToInt32(lengthBytes, 0);

        // читаємо байти по кількості, що відповідає розміру


        byte[] buffer = new byte[1024];
        List<byte> allBytes = new List<byte>();

        int totalBytesRead = 0;
        int bytesRead;
        do
        {
            var estimatedBytesToRead = Math.Min(buffer.Length, messageLength - totalBytesRead);
            bytesRead = await stream.ReadAsync(buffer, 0, estimatedBytesToRead);
            allBytes.AddRange(buffer.Take(bytesRead));
            totalBytesRead += bytesRead;
        } while (totalBytesRead < messageLength);

        // перетворюємо байти у рядок
        return Encoding.UTF8.GetString(allBytes.ToArray());
    }

    // bool
    public static async Task SendBoolAsync(bool value, NetworkStream stream)
    {
        var boolBytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(boolBytes, 0, boolBytes.Length);
    }

    public static async Task<bool> ReceiveBoolAsync(NetworkStream stream)
    {
        byte[] boolBytes = new byte[sizeof(bool)];
        await stream.ReadAsync(boolBytes, 0, boolBytes.Length);
        return BitConverter.ToBoolean(boolBytes, 0);
    }

    // int
    public static async Task SendIntAsync(int value, NetworkStream stream)
    {
        var intBytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(intBytes, 0, intBytes.Length);
    }

    public static async Task<int> ReceiveIntAsync(NetworkStream stream)
    {
        byte[] intBytes = new byte[sizeof(int)];
        await stream.ReadAsync(intBytes, 0, intBytes.Length);
        return BitConverter.ToInt32(intBytes, 0);
    }

    // long
    public static async Task SendLongAsync(long value, NetworkStream stream)
    {
        var longBytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(longBytes, 0, longBytes.Length);
    }

    public static async Task<long> ReceiveLongAsync(NetworkStream stream)
    {
        byte[] longBytes = new byte[sizeof(long)];
        await stream.ReadAsync(longBytes, 0, longBytes.Length);
        return BitConverter.ToInt64(longBytes, 0);
    }

    // float
    public static async Task SendFloatAsync(float value, NetworkStream stream)
    {
        var floatBytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(floatBytes, 0, floatBytes.Length);
    }

    public static async Task<float> ReceiveFloatAsync(NetworkStream stream)
    {
        byte[] floatBytes = new byte[sizeof(float)];
        await stream.ReadAsync(floatBytes, 0, floatBytes.Length);
        return BitConverter.ToSingle(floatBytes, 0);
    }
    // double
    public static async Task SendDoubleAsync(double value, NetworkStream stream)
    {
        var doubleBytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(doubleBytes, 0, doubleBytes.Length);
    }

    public static async Task<double> ReceiveDoubleAsync(NetworkStream stream)
    {
        byte[] doubleBytes = new byte[sizeof(double)];
        await stream.ReadAsync(doubleBytes, 0, doubleBytes.Length);
        return BitConverter.ToDouble(doubleBytes, 0);
    }

    // object
    public static async Task SendObjectAsync<T>(T obj, NetworkStream stream)
    {
        string jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
        await SendStringAsync(jsonString, stream);
    }

    public static async Task<T?> ReceiveObjectAsync<T>(NetworkStream stream)
    {
        string jsonString = await ReceiveStringAsync(stream);
        return System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
    }

    // file
    public static async Task SendFileAsync(string filePath, NetworkStream stream)
    {
        // передаємо спочатку ім'я файлу
        var fileName = Path.GetFileName(filePath);
        await SendStringAsync(fileName, stream);

        using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        long fileSize = fileStream.Length;
        // відправляємо розмір файлу
        await SendLongAsync(fileSize, stream);

        // в циклі через буфер
        // потім відправляємо вміст файлу
        //byte[] buffer = new byte[8192];
        //int bytesRead;
        //while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        //{
        //    await stream.WriteAsync(buffer, 0, bytesRead);
        //}
        // або можна просто скопіювати потік файлу у потік мережі
        await fileStream.CopyToAsync(stream);
    }

    public static async Task<(string, long)> ReceiveFileAsync(string saveDirectory, NetworkStream stream)
    {
        // спочатку отримуємо ім'я файлу
        string fileName = await ReceiveStringAsync(stream);
        string savePath = Path.Combine(saveDirectory, fileName);
        // отримуємо розмір файлу
        long fileSize = await ReceiveLongAsync(stream);
        // приймаємо вміст файлу
        using FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);
        byte[] buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;
        while (totalBytesRead < fileSize && (bytesRead = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, fileSize - totalBytesRead))) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
        }

        return (savePath, fileSize);
    }


}

public class User
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;


    public static async Task<RegisterResult> RegisterUserAsync(User user, string filePath)
    {
        var users = await LoadUsersAsync(filePath);
        var isExistingUser = users.Any(u => u.Login == user.Login);
        var result = new RegisterResult();
        if (isExistingUser)
        {
            result.IsRegistered = false;
            result.Message = "User already exists.";
            return result;
        }
        users.Add(user);

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, users);

        result = new RegisterResult();
        result.IsRegistered = true;
        result.Message = "User registered successfully.";
        return result;
    }

    public static async Task<AuthResult> AuthenticateUserAsync(User user, string filePath)
    {
        var users = await LoadUsersAsync(filePath);
        var authenticatedUser = users.FirstOrDefault(u => u.Login == user.Login && u.Password == user.Password);
        var result = new AuthResult
        {
            IsAuthenticated = authenticatedUser != null,
            Message = authenticatedUser != null ? "Authentication successful." : "Invalid login or password."
        };
        return result;
    }

    public static async Task<List<User>> LoadUsersAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new List<User>();
        }
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var users = await JsonSerializer.DeserializeAsync<List<User>>(fileStream);
        if (users == null)
        {
            return new List<User>();
        }
        return users;
    }

}

public class AuthResult
{
    public bool IsAuthenticated { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class RegisterResult
{
    public bool IsRegistered { get; set; }
    public string Message { get; set; } = string.Empty;
}


public class FileReceiveResult {
    public bool IsSuccess { get; set; }
    public long FileSize { get; set; }
    public string FullPath { get; set; } = string.Empty;
}

public class MessageNotification
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public DateTime SendingDate { get; set; }
}

public class Message
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime SendingDate { get; set; }
    public bool IsDelivered { get; set; } = false;
}