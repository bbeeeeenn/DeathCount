using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Configuration;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TShockAPI.Net;

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

                    while (query.Read())
                    {
                        string username = query.Reader.Get<string>("Username");
                        int deathcount = query.Reader.Get<int>("Deaths");

                        args.Player.SendInfoMessage($"{username}: {deathcount}");
                    }
                }
            }
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
                    $"[Death Counter DB] Created new death counter row for {account.Name}"
                );
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

            using QueryResult queryReader = TShock.DB.QueryReader(
                $@"
                    UPDATE Deathcount
                    SET Deaths=Deaths + 1
                    WHERE Username='{Username}' AND World={Main.worldID}
                    RETURNING Deaths;
                "
            );

            if (!queryReader.Read())
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
