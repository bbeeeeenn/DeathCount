using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace DeathCount
{
    [ApiVersion(2, 1)]
    public class DeathCount : TerrariaPlugin
    {
        public override string Name => "Death Counter";
        public override string Author => "TRANQUILZOIIP - github.com/bbeeeeenn";
        public override string Description => base.Description;
        public override Version Version => base.Version;

        public DeathCount(Main game)
            : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
            PlayerHooks.PlayerPostLogin += OnPlayerLogin;
            Commands.ChatCommands.Add(new Command("tshock.canchat", ViewDeath, "death"));

            InitializeDB();
        }

        private static void InitializeDB()
        {
            TShock.DB.Query(
                @"
                    CREATE TABLE IF NOT EXISTS Deathcount (
                    World INTEGER NOT NULL,
                    WorldName TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    Deaths INTEGER DEFAULT 0
                    );
                "
            );
        }

        private void OnPlayerLogin(PlayerPostLoginEventArgs e)
        {
            UserAccount account = e.Player.Account;
            using QueryResult reader = TShock.DB.QueryReader(
                $@"
                    SELECT * FROM Deathcount
                    WHERE World={Main.worldID} AND Username='{account.Name}'
                    LIMIT 1;
                "
            );

            if (!reader.Read())
            {
                TShock.DB.Query(
                    $@"
                        INSERT INTO Deathcount (World, Worldname, Username)
                        VALUES ({Main.worldID}, '{Main.worldName}', '{account.Name}');
                    "
                );
                TShock.Log.ConsoleInfo(
                    $"[Death Counter DB] Created new death counter row for {account.Name}."
                );
            }
        }

        private void ViewDeath(CommandArgs args)
        {
            if (args.Player != null && args.Player.Active)
            {
                if (args.Parameters.Count == 0)
                {
                    using QueryResult query = TShock.DB.QueryReader(
                        $@"
                        SELECT Deaths FROM Deathcount
                        WHERE World={Main.worldID} AND Username='{args.Player.Account.Name}'
                        LIMIT 1;
                    "
                    );
                    if (query.Read())
                    {
                        int death = query.Reader.Get<int>("Deaths");
                        args.Player.SendInfoMessage(
                            $"You died {death} {(death == 1 ? "time" : "times")}."
                        );
                    }
                }
                else if (args.Parameters[0].ToLower() == "all")
                {
                    using QueryResult query = TShock.DB.QueryReader(
                        $@"
                        SELECT Username, Deaths FROM Deathcount
                        WHERE World={Main.worldID}
                        ORDER BY Deaths DESC, Username ASC;
                        "
                    );
                    Dictionary<string, int> playerdeaths = new();
                    List<string> deathstrings = new() { "--- Death Counts ---" };

                    while (query.Read())
                    {
                        string username = query.Reader.Get<string>("Username");
                        int deathcount = query.Reader.Get<int>("Deaths");
                        playerdeaths[username] = deathcount;
                    }

                    foreach (string username in playerdeaths.Keys)
                    {
                        string? playername = TShock
                            .Players.FirstOrDefault(player => player?.Account?.Name == username)
                            ?.Name;

                        deathstrings.Add(
                            $"{username}{(playername != null ? $"({playername})" : "")}: {playerdeaths[username]}"
                        );
                    }

                    args.Player.SendInfoMessage(string.Join("\n", deathstrings));
                }
            }
        }

        private void OnNetGetData(GetDataEventArgs args)
        {
            if (args.MsgID == PacketTypes.PlayerDeathV2)
                CountDeath(args);
        }

        private static void CountDeath(GetDataEventArgs args)
        {
            using var reader = new BinaryReader(
                new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)
            );
            var playerId = reader.ReadByte();

            string Username = TShock.Players[playerId].Account.Name;

            int queryAffected = TShock.DB.Query(
                $@"
                    UPDATE Deathcount
                    SET Deaths=Deaths + 1
                    WHERE Username='{Username}' AND World={Main.worldID};
                "
            );

            if (queryAffected < 1)
            {
                TShock.Utils.Broadcast(
                    $"[Death Counter ERROR] Death of {Username} was not recorded.",
                    Color.Beige
                );
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnNetGetData);
                PlayerHooks.PlayerPostLogin -= OnPlayerLogin;
            }
            base.Dispose(disposing);
        }
    }
}
