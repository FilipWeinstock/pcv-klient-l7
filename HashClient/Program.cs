using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

class Program
{
    const string Host = "127.0.0.1";
    const int Port = 9000;

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Pouzitie:");
            Console.WriteLine("  list");
            Console.WriteLine("  get <hash>");
            Console.WriteLine("  upload <subor> <description>");
            Console.WriteLine("  delete <hash>");
            return;
        }

        string command = args[0].ToLower();

        try
        {
            using (var client = new TcpClient())
            {
                client.Connect(Host, Port);
                using (var stream = client.GetStream())
                {
                    switch (command)
                    {
                        case "list":
                            DoList(stream);
                            break;
                        case "get":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("Chyba: get <hash>");
                                return;
                            }
                            DoGet(stream, args[1]);
                            break;
                        case "upload":
                            if (args.Length < 3)
                            {
                                Console.WriteLine("Chyba: upload <subor> <description>");
                                return;
                            }
                            string filePath = args[1];
                            string description = string.Join(" ", args, 2, args.Length - 2);
                            DoUpload(stream, filePath, description);
                            break;
                        case "delete":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("Chyba: delete <hash>");
                                return;
                            }
                            DoDelete(stream, args[1]);
                            break;
                        default:
                            Console.WriteLine("Neznamy prikaz.");
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Chyba pripojenia alebo komunikacie: " + ex.Message);
        }
    }

    static void WriteLine(NetworkStream stream, string line)
    {
        byte[] data = Encoding.UTF8.GetBytes(line + "\n");
        stream.Write(data, 0, data.Length);
    }

    static string ReadLine(NetworkStream stream)
    {
        using (var ms = new MemoryStream())
        {
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new IOException("Spojenie ukoncene.");
                if (b == '\n')
                    break;
                ms.WriteByte((byte)b);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    static byte[] ReadExact(NetworkStream stream, int length)
    {
        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = stream.Read(buffer, offset, length - offset);
            if (read <= 0)
                throw new IOException("Nedokazal som precitat vsetky data.");
            offset += read;
        }
        return buffer;
    }

    //LIST

    static void DoList(NetworkStream stream)
    {
        WriteLine(stream, "LIST");
        string status = ReadLine(stream);
        Console.WriteLine(status);

        var parts = status.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] != "200")
            return;

        if (!int.TryParse(parts[2], out int count))
            return;

        for (int i = 0; i < count; i++)
        {
            string line = ReadLine(stream);
            Console.WriteLine(line);
        }
    }

    //GET

    static void DoGet(NetworkStream stream, string hash)
    {
        WriteLine(stream, $"GET {hash}");
        string status = ReadLine(stream);
        Console.WriteLine(status);

        var parts = status.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return;

        if (parts[0] == "404")
        {
            Console.WriteLine("Subor neexistuje.");
            return;
        }

        if (parts[0] != "200" || parts.Length < 4)
        {
            Console.WriteLine("Neocakavana odpoved.");
            return;
        }

        if (!int.TryParse(parts[2], out int length))
        {
            Console.WriteLine("Zly format length.");
            return;
        }

        string description = parts[3];
        Console.WriteLine($"Stahujem {length} bajtov, popis: {description}");

        byte[] data = ReadExact(stream, length);

        string fileName = "down_" + hash;
        File.WriteAllBytes(fileName, data);
        Console.WriteLine($"Ulozene do suboru: {fileName}");
    }

    //UPLOAD

    static void DoUpload(NetworkStream stream, string filePath, string description)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Subor neexistuje: " + filePath);
            return;
        }

        byte[] data = File.ReadAllBytes(filePath);
        int length = data.Length;

        string header = $"UPLOAD {length} {description}";
        WriteLine(stream, header);
        stream.Write(data, 0, data.Length);

        string status = ReadLine(stream);
        Console.WriteLine(status);
    }

    //DELETE

    static void DoDelete(NetworkStream stream, string hash)
    {
        WriteLine(stream, $"DELETE {hash}");
        string status = ReadLine(stream);
        Console.WriteLine(status);
    }
}
