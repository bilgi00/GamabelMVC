// PowerShell script to query admin_kullanicilar table
using MySqlConnector;
using System;

var cs = "Server=localhost;Database=u2636310_dbE97;User=u2636310_userE97;Password=VQLjJ1hmp3VdOQTp;SslMode=Preferred;";

try {
    using var conn = new MySqlConnection(cs);
    conn.OpenAsync().Wait();
    
    using var cmd = new MySqlCommand("SELECT COUNT(*) FROM admin_kullanicilar", conn);
    var count = cmd.ExecuteScalar();
    Console.WriteLine($"Total users in admin_kullanicilar: {count}");
    
    using var selectCmd = new MySqlCommand("SELECT id, kullanici_adi, birim, rol FROM admin_kullanicilar LIMIT 20", conn);
    using var reader = selectCmd.ExecuteReader();
    
    while (reader.Read()) {
        Console.WriteLine($"ID: {reader[0]}, User: {reader[1]}, Birim: {reader[2]}, Rol: {reader[3]}");
    }
    
    conn.Close();
} 
catch (Exception ex) {
    Console.WriteLine($"Error: {ex}");
}
