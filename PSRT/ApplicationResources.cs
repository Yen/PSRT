using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    class ApplicationResources
    {
        private Dictionary<string, string> _BlockNameTranslations = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> BlockNameTranslations => _BlockNameTranslations;

        public IPAddress BindAddress { get; private set; } = IPAddress.Loopback;
        public IPAddress HostAddress { get; private set; } = IPAddress.Loopback;

        public async Task LoadResources()
        {
            using (var connection = new SQLiteConnection("Data Source=Resources/Translations.sqlite; Version=3;"))
            {
                await connection.OpenAsync();

                using (var command = new SQLiteCommand("select original, replacement from block_names where replacement is not null", connection))
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync())
                        _BlockNameTranslations[(string)reader["original"]] = (string)reader["replacement"];
            }
        }
    }
}
