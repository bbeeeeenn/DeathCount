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
            if (e.Player == null)
                return;
            UserAccount account = e.Player.Account;
            try
            {
                using QueryResult reader = TShock.DB.QueryReader(
                    $@"
                    SELECT * FROM Deathcount
                    WHERE World=@0 AND Username=@1
                    LIMIT 1;
                ",
                    Main.worldID,
                    account.Name
                );

                if (!reader.Read())
                {
                    TShock.DB.Query(
                        $@"
                        INSERT INTO Deathcount (World, Worldname, Username)
                        VALUES (@0, @1, @2);
                    ",
                        Main.worldID,
                        Main.worldName,
                        account.Name
                    );
                    TShock.Log.ConsoleInfo(
                        $"[Death Counter] New record created for {account.Name}."
                    );
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"[Death Counter] Error handling player login for {account.Name}: {ex.Message}"
                );
            }
        }

        private void ViewDeath(CommandArgs args)
        {
            TSPlayer player = args.Player;

            if (
                player == null
                || !player.Active
                || (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "all")
            )
            {
                using QueryResult query = TShock.DB.QueryReader(
                    $@"
                        SELECT Username, Deaths FROM Deathcount
                        WHERE World=@0
                        ORDER BY Deaths ASC;
                        ",
                    Main.worldID
                );
                Dictionary<string, int> playerdeaths = new();
                List<string> deathstrings = new() { "--- Death Counts ---" };

                while (query.Read())
                {
                    string username = query.Reader.Get<string>("Username");
                    int deathcount = query.Reader.Get<int>("Deaths");
                    if (deathcount <= 0)
                        continue;
                    playerdeaths[username] = deathcount;
                }

                foreach (string username in playerdeaths.Keys)
                {
                    string? playername = TShock
                        .Players.FirstOrDefault(player => player?.Account?.Name == username)
                        ?.Name;

                    deathstrings.Add(
                        $"{username}{(playername != null ? $"({playername})" : "")} - {playerdeaths[username]}"
                    );
                }

                if (player != null && player.Active)
                {
                    player.SendMessage(string.Join("\n", deathstrings), Color.Green);
                }
                else
                {
                    TShock.Log.ConsoleInfo(string.Join("\n", deathstrings));
                }
                return;
            }

            if (args.Parameters.Count == 0)
            {
                using QueryResult query = TShock.DB.QueryReader(
                    $@"
                        SELECT Deaths FROM Deathcount
                        WHERE World=@0 AND Username=@1
                        LIMIT 1;
                    ",
                    Main.worldID,
                    args.Player.Account.Name
                );
                if (query.Read())
                {
                    int death = query.Reader.Get<int>("Deaths");
                    args.Player.SendMessage(
                        $"You have died {death} {(death == 1 ? "time" : "times")}.\nType '/death all' to see everyone's death counts.",
                        Color.Green
                    );
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
                    WHERE Username=@0 AND World=@1;
                ",
                Username,
                Main.worldID
            );

            if (queryAffected < 1)
            {
                TShock.Utils.Broadcast(
                    $"[Death Counter] Failed to record death for {Username}.",
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
