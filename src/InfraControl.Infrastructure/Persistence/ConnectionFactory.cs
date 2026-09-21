using System.Data;
using InfraControl.Application.Common.Interfaces;
using Npgsql;

namespace InfraControl.Infrastructure.Persistence;

public class ConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;

    public ConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
