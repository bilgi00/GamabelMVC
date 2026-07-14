[System.Reflection.Assembly]::LoadFrom("C:\Users\TX120\.nuget\packages\mysqlconnector\8.4.0\lib\net8.0\MySqlConnector.dll") | Out-Null

$connectionString = "Server=localhost;Database=u2636310_dbE97;User=u2636310_userE97;Password=VQLjJ1hmp3VdOQTp;SslMode=Preferred;"
$connection = New-Object MySqlConnector.MySqlConnection($connectionString)
$connection.OpenAsync().Wait()

$command = $connection.CreateCommand()
$command.CommandText = "DESCRIBE admin_kullanicilar"
$reader = $command.ExecuteReaderAsync().Result

while($reader.ReadAsync().Result) {
  Write-Host $reader[0] "| " $reader[1]
}

$reader.Close()
$connection.Close()
