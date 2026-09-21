using System.Data;

namespace InfraControl.Application.Common.Interfaces;

public interface IConnectionFactory
{
    IDbConnection CreateConnection();
}
