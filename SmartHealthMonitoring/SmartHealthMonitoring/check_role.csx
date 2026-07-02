using Microsoft.Data.SqlClient;

var connStr = @"Server=.\SQLEXPRESS;Database=HeartCareDB_AI_Focus;Trusted_Connection=True;TrustServerCertificate=True";
using var conn = new SqlConnection(connStr);
conn.Open();
using var cmd = new SqlCommand("SELECT Id, Email, Role, IsDeleted FROM Users WHERE Email LIKE '%mai%'", conn);
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"Id={reader["Id"]}, Email={reader["Email"]}, Role={reader["Role"]}, IsDeleted={reader["IsDeleted"]}");
}
