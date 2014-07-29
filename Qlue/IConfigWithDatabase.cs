using System;

namespace Qlue
{
    public interface IConfigWithDatabase : IConfig
    {
        string GetDatabaseConnectionString();
    }
}
