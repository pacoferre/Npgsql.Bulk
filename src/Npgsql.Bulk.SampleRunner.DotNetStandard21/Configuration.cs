using Microsoft.Extensions.Configuration;

namespace Npgsql.Bulk.SampleRunner.DotNetStandard21
{
    public static class Configuration
    {
        public const string ConnectionString =
            "server=localhost;user id=postgres;password=qwerty;database=copy;port=5432";
    }
}