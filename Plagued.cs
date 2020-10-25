//#define DEBUG
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Plagued", "Psi|ocybin/RFC1920", "0.3.7", ResourceId = 1991)]
    [Description("Everyone is infected")]

    class Plagued : RustPlugin
    {
        #region Initialization
        [PluginReference]
        private Plugin Friends, Clans;

        public static Plagued Plugin;
        static int plagueRange = 20;
        static int plagueIncreaseRate = 50;
        static int plagueDecreaseRate = 30;
        static int plagueMinAffinity = 3000;
        static int affinityIncRate = 100;
        static int affinityDecRate = 60;
        static int maxKin = 2;
        static int maxKinChanges = 3;
        static int playerLayer;
        static bool disableSleeperAffinity = false;
        static bool UseFriends, UseClans, UseTeams = false;
        static bool friendsAutoKin = false;

        Dictionary<ulong, PlayerState> playerStates = new Dictionary<ulong, PlayerState>();
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region Hooks
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");

            Config.Clear();
            Config["plagueRange"] = 20;
            Config["plagueIncreaseRate"] = 5;
            Config["plagueDecreaseRate"] = 1;
            Config["plagueMinAffinity"] = 6000;
            Config["affinityIncRate"] = 10;
            Config["affinityDecRate"] = 1;
            Config["maxKin"] = 2;
            Config["maxKinChanges"] = 3;
            Config["disableSleeperAffinity"] = false;
            Config["UseFriends"] = false;
            Config["UseClans"] = false;
            Config["UseTeams"] = false;
            Config["friendsAutoKin"] = false;

            SaveConfig();
        }

        void Init()
        {
            AddCovalenceCommand("plagued", "CmdPlagued");
            PlayerState.setupDatabase(this);
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "HELP0", "Welcome to plagued mod. Try the <color=#81F781>/plagued</color> command for more information." },
                { "HELP1", "<color=#81F781>/plagued addkin</color> => <color=#D8D8D8> Add the player you are looking at to your kin list.</color>" },
                { "HELP2", "<color=#81F781>/plagued delkin</color> => <color=#D8D8D8> Remove the player you are looking at from your kin list.</color>" },
                { "HELP3", "<color=#81F781>/plagued delkin</color> <color=#F2F5A9> number </color> => <color=#D8D8D8> Remove a player from your kin list by kin number.</color>" },
                { "HELP4", "<color=#81F781>/plagued lskin</color> => <color=#D8D8D8> Display your kin list.</color>" },
                { "HELP5", "<color=#81F781>/plagued lsassociates</color> => <color=#D8D8D8> Display your associates list.</color>" },
                { "HELP6", "<color=#81F781>/plagued info</color> => <color=#D8D8D8> Display information about the workings of this mod.</color>" },
                { "INFO1", " ===== Plagued mod ======" },
                { "INFO2", "COVID 19 has decimated most of the population. You find yourself on a deserted island, lucky to be among the few survivors. But the biological apocalypse is far from being over. It seems that the virus starts to express itself when certain hormonal changes are triggered by highly social behaviors. It has been noted that small groups of survivor seems to be relatively unaffected, but there isn't one single town or clan that wasn't decimated." },
                { "INFO3", "Workings:\n The longer you hang around others, the sicker you'll get. However, your kin are unaffected, add your friends as kin and you will be able to collaborate. Choose your kin wisely, there are no big families in this world." },
                { "INFO4", "Settings:\n > Max kin : {0}\n > Max kin changes / Restart : {1}" },
                { "kinmustbevalid", "Kin position must be a valid number!" },
                { "invalid", "Invalid Plagued mod command." },
                { "nowkin", "You are now kin with {0}!" },
                { "haveno", "You have no {0}." },
                { "already", "{0} is already your kin!" },
                { "yourequest", "You have requested to be {0}'s kin!" },
                { "theyrequest", "{0} has requested to be your kin.  Add them back to become kin!" },
                { "cannotadd", "{0} could not be added to kin!" },
                { "cannotremove",  "{0} could not be removed from kin list (Exceeded max kin changes per restart)!" },
                { "cannotremoves",  "Could not remove kin." },
                { "removedkin", "{0} was removed from your kin list!" },
                { "removedkins",  "Removed kin." },
                { "notkin", "{0} is not your kin!" },
                { "rotfail", "Couldn't get player rotation." },
                { "nolook", "You aren't looking at a player." },
                { "playermsg1", "I don't feel so well." },
                { "playermsg2", "I feel much better now." }
            }, this, "en");

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
                if (proximityDetector != null)
                {
                    UnityEngine.Object.Destroy(proximityDetector);
                }
                player.gameObject.AddComponent<ProximityDetector>();
                if (!playerStates.ContainsKey(player.userID))
                {
                    playerStates.Add(player.userID, new PlayerState(player, null));
                }
            }
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
#if DEBUG
                Puts($"Trying to remove proximity detector from {player.displayName}.");
#endif
                ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
                if (proximityDetector != null)
                {
#if DEBUG
                    Puts($"Removing proximity detector from {player.displayName}.");
#endif
                    UnityEngine.Object.Destroy(proximityDetector);
                }
            }
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
#if DEBUG
                Puts($"Trying to remove proximity detector from {player.displayName}.");
#endif
                ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
                if (proximityDetector != null)
                {
#if DEBUG
                    Puts($"Removing proximity detector from {player.displayName}.");
#endif
                    UnityEngine.Object.Destroy(proximityDetector);
                }
            }
            PlayerState.closeDatabase();
        }

        void OnServerInitialized()
        {
            Plugin = this; // For debug and lang()
            // Set the layer that will be used in the radius search. We only want human players in this case.
            playerLayer = LayerMask.GetMask("Player (Server)");

            // Reload the player states
            playerStates = new Dictionary<ulong, PlayerState>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
#if DEBUG
                Puts($"Adding plague state to {player.displayName}");
#endif
                playerStates.Add(player.userID, new PlayerState(player, null));
            }

            plagueRange        = (int)Config["plagueRange"];
            plagueIncreaseRate = (int)Config["plagueIncreaseRate"];
            plagueDecreaseRate = (int)Config["plagueDecreaseRate"];
            plagueMinAffinity  = (int)Config["plagueMinAffinity"];
            affinityIncRate    = (int)Config["affinityIncRate"];
            affinityDecRate    = (int)Config["affinityDecRate"];
            maxKin             = (int)Config["maxKin"];
            maxKinChanges      = (int)Config["maxKinChanges"];
            disableSleeperAffinity = (bool)Config["disableSleeperAffinity"];
        }

        void OnPlayerConnected(BasePlayer player)
        {
            // Add the player to the player state list
            if (!playerStates.ContainsKey(player.userID))
            {
                // The player was loaded in the current game session
                playerStates.Add(player.userID, new PlayerState(player, null));
                Message(player.IPlayer, "HELP0");
#if DEBUG
                Puts(player.displayName + " has been plagued!");
#endif
            }
            // Add the proximity detector to the player
            player.gameObject.AddComponent<ProximityDetector>();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
            if (proximityDetector != null)
            {
#if DEBUG
                Puts($"Removing proximity detector from {player.displayName}.");
#endif
                UnityEngine.Object.Destroy(proximityDetector);
            }
#if DEBUG
            Puts(player.displayName + " is no longer watched!");
#endif
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism)
        {
            // 0 - 1000 -> Decreased Health Regen
            // 1000 - 2000 -> Increased hunger
            // 2000 - 3000 -> Increased thirst
            // 3000 - 4000 -> No Health Regen
            // 4000 - 5000 -> No comfort
            // 5000 - 6000 -> Increased Hunger 2
            // 6000 - 7000 -> Increased Thirst 2
            // 7000 - 8000 -> Cold
            // 8000 - 9000 -> Bleeding
            // 9000+ -> Poison

            /*
             * -- ----------------------------
             * -- Rust default rates
             * -- ----------------------------
             * -- healthgain = 0.03
             * -- caloriesloss = 0 - 0.05
             * -- hydrationloss = 0 - 0.025
             * -- ----------------------------
             */
            BasePlayer player = metabolism.GetComponent<BasePlayer>();
            PlayerState state = playerStates[player.userID];
            int plagueLevel = state.getPlagueLevel();
            float defaultHealthGain = 0.03f;
            float defaultCaloriesLoss = 0.05f;
            float defaultHydrationLoss = 0.025f;
#if DEBUG
            Puts("Infection stage " + (plagueLevel / 1000).ToString());
#endif
//            if (plagueLevel == 0) return;

            if (plagueLevel <= 1) return;
            //Puts("Infection stage 1 " + player.displayName + " " + player.userID);
            metabolism.pending_health.value = metabolism.pending_health.value + (defaultHealthGain / 2f);

            if (plagueLevel <= 1000) return;
            //Puts("Infection stage 2");
            metabolism.calories.value = metabolism.calories.value - ((defaultCaloriesLoss * 3f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 2000) return;
            //Puts("Infection stage 3");
            metabolism.hydration.value = metabolism.hydration.value - ((defaultHydrationLoss * 3f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 3000) return;
            metabolism.pending_health.value = metabolism.pending_health.value - (defaultHealthGain / 2f);

            if (plagueLevel <= 4000) return;
            //Puts("Infection stage 5");
            metabolism.comfort.value = -1;

            if (plagueLevel <= 5000) return;
            //Puts("Infection stage 6");
            metabolism.calories.value = metabolism.calories.value - ((defaultCaloriesLoss * 5f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 6000) return;
            //Puts("Infection stage 7");
            metabolism.hydration.value = metabolism.hydration.value - ((defaultHydrationLoss * 5f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 7000) return;
            ///Puts("Infection stage 8");
            metabolism.temperature.value -= 0.05f;

            if (plagueLevel <= 8000) return;
            //Puts("Infection stage 9");
            metabolism.bleeding.value += 0.2f;
            metabolism.radiation_poison.value += 1f;

            if (plagueLevel < 10000) return;
            //Puts("Infection stage 10");
            metabolism.poison.value += 1.5f;
            metabolism.radiation_level.value += 1.5f;
        }

        // OUR HOOKS
        void OnPlayerProximity(BasePlayer player, List<BasePlayer> players)
        {
            if (playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID].increasePlaguePenalty(players);
#if DEBUG
                Puts($"{player.displayName} is close to {(players.Count - 1).ToString()} other players!");
                foreach (BasePlayer pl in players)
                {
                    Puts($"{pl.displayName}");
                }
#endif
            }
        }

        void OnPlayerAlone(BasePlayer player)
        {
            if (playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID].decreasePlaguePenalty();
#if DEBUG
                Puts($"OnPlayerAlone: {player.userID}");
#endif
            }
        }
        // END - OUR HOOKS
        #endregion

        #region Commands
        [Command("plagued")]
        private void CmdPlagued(IPlayer iplayer, string command, string[] args)
        {
            if (args.Length == 0)
            {
                Message(iplayer, "HELP1");
                Message(iplayer, "HELP2");
                Message(iplayer, "HELP3");
                Message(iplayer, "HELP4");
                Message(iplayer, "HELP5");
                Message(iplayer, "HELP6");
                return;
            }

            if (args.Length >= 1)
            {
                switch (args[0])
                {
                    case "addkin":
                        cmdAddKin(iplayer);
                        break;
                    case "delkin":
                        if (args.Length == 2)
                        {
                            int position;
                            if (int.TryParse(args[1], out position))
                            {
                                cmdDelKin(iplayer, position);
                            }
                            else
                            {
                                Message(iplayer, "kinmustbevalid");
                            }
                        }
                        else
                        {
                            cmdDelKin(iplayer);
                        }
                        break;
                    case "lskin":
                        cmdListKin(iplayer);
                        break;
                    case "lsassociates":
                        cmdListAssociates(iplayer);
                        break;
                    case "info":
                        cmdInfo(iplayer);
                        break;
                    default:
                        Message(iplayer, "invalid");
                        break;
                }
            }
        }

        void cmdAddKin(IPlayer iplayer)
        {
            var player = iplayer.Object as BasePlayer;
            BasePlayer targetPlayer;

            if (getPlayerLookedAt(player, out targetPlayer))
            {
#if DEBUG
                Puts($"Looking at player {targetPlayer.userID}");
#endif
                PlayerState state = playerStates[player.userID];
                PlayerState targetPlayerState;
                playerStates.TryGetValue(targetPlayer.userID, out targetPlayerState);

                if (state.isKinByUserID(targetPlayer.userID))
                {
                    Message(iplayer, "already", targetPlayer.displayName);
                    return;
                }
#if DEBUG
                Puts($"Trying to add player {targetPlayer.userID} to kin...");
#endif
                if (playerStates.ContainsKey(targetPlayer.userID))
                {
                    if (state.hasKinRequest(targetPlayer.userID))
                    {
                        state.addKin(targetPlayer.userID);
                        targetPlayerState.addKin(player.userID);
                        Message(iplayer, "nowkin", targetPlayer.displayName);
                        Message(targetPlayer.IPlayer, "nowkin", player.displayName);

                        return;
                    }
                    else
                    {
                        targetPlayerState.addKinRequest(player.userID);
                        Message(iplayer, "yourequest", targetPlayer.displayName);
                        Message(targetPlayer.IPlayer, "theyrequest", player.displayName);

                        return;
                    }
                }

                Message(iplayer, "cannotadd", targetPlayer.displayName);
            }
        }

        bool cmdDelKin(IPlayer iplayer)
        {
            var player = iplayer.Object as BasePlayer;
            BasePlayer targetPlayer;

            if (getPlayerLookedAt(player, out targetPlayer))
            {
                PlayerState state = playerStates[player.userID];
                PlayerState targetPlayerState = playerStates[targetPlayer.userID];

                if (!state.isKinByUserID(targetPlayer.userID))
                {
                    Message(iplayer, "notkin", targetPlayer.displayName);

                    return false;
                }

                if (state.removeKin(targetPlayer.userID) && targetPlayerState.forceRemoveKin(player.userID))
                {
                    Message(iplayer, "removedkin", targetPlayer.displayName);
                    Message(targetPlayer.IPlayer, "removedkin", iplayer.Name);

                    return true;
                }

                Message(iplayer, "cannotremove", targetPlayer.displayName);
            }

            return false;
        }

        bool cmdDelKin(IPlayer iplayer, int id)
        {
            var player = iplayer.Object as BasePlayer;
            PlayerState state = playerStates[player.userID];

            if (state.removeKinById(id))
            {
                foreach (var item in playerStates)
                {
                    if (item.Value.getId() == id)
                    {
                        item.Value.forceRemoveKin(player.userID);
                    }
                }
                Message(iplayer, "removedkins");
            }
            else
            {
                Message(iplayer, "cannotremoves");
            }

            return false;
        }

        void cmdListKin(IPlayer iplayer)
        {
            var player = iplayer.Object as BasePlayer;
            List<string> kinList = playerStates[player.userID].getKinList();

            displayList(iplayer, "Kin", kinList);
        }

        void cmdListAssociates(IPlayer iplayer)
        {
            var player = iplayer.Object as BasePlayer;
            List<string> associatesList = playerStates[player.userID].getAssociatesList();
            displayList(iplayer, "Associates", associatesList);
        }

        bool cmdInfo(IPlayer iplayer)
        {
            Message(iplayer, "INFO1");
            Message(iplayer, "INFO2");
            Message(iplayer, "INFO3");
            Message(iplayer, "INFO4", maxKin.ToString(), maxKinChanges.ToString());
            return false;
        }
        #endregion

        #region Helpers
        // Send chat message as player
        public static void MsgPlayer(BasePlayer player, string format, params object[] args)
        {
            if (player?.net != null) player.SendConsoleCommand("chat.say", (args.Length > 0) ? "/" + string.Format(format, args) : "/" + format, 1f);
        }

        public void displayList(IPlayer iplayer, string listName, List<string> stringList)
        {
            if (stringList.Count == 0)
            {
                Message(iplayer, "haveno", listName.ToLower());
                return;
            }

            string answerMsg = listName + " list: \n";

            foreach (string text in stringList)
            {
                answerMsg += "> " + text + "\n";
            }

            Message(iplayer, answerMsg);
        }

        bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (UseFriends && Friends != null)
            {
#if DEBUG
                Puts("Checking Friends...");
#endif
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if(fr != null && (bool)fr)
                {
#if DEBUG
                    Puts("  IsFriend: true based on Friends plugin");
#endif
                    return true;
                }
            }
            if(UseClans && Clans != null)
            {
#if DEBUG
                Puts("Checking Clans...");
#endif
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan  = (string)Clans?.CallHook("GetClanOf", ownerid);
                if(playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
#if DEBUG
                    Puts("  IsFriend: true based on Clans plugin");
#endif
                    return true;
                }
            }
            if(UseTeams)
            {
#if DEBUG
                Puts("Checking Rust teams...");
#endif
                BasePlayer player = BasePlayer.FindByID(playerid);
                if(player.currentTeam != (long)0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
                    if(playerTeam == null) return false;
                    if(playerTeam.members.Contains(ownerid))
                    {
#if DEBUG
                        Puts("  IsFriend: true based on Rust teams");
#endif
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Geometry
        bool getPlayerLookedAt(BasePlayer player, out BasePlayer targetPlayer)
        {
            targetPlayer = null;

            Quaternion currentRot;
            if (!TryGetPlayerView(player, out currentRot))
            {
                Message(player.IPlayer, "rotfail");
                return false;
            }

            object closestEnt;
            Vector3 closestHitpoint;
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return false;
            targetPlayer = ((Collider)closestEnt).GetComponentInParent<BasePlayer>();

            if (targetPlayer == null)
            {
                Message(player.IPlayer, "nolook");
                return false;
            }

            return true;
        }

        bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            /*
             * Credit: Nogrod (HumanNPC)
             */
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider.GetComponentInParent<TriggerBase>() == null && hit.distance < closestdist)
                {
                    closestdist = hit.distance;
                    closestEnt = hit.collider;
                    closestHitpoint = hit.point;
                }
            }

            if (closestEnt is bool) return false;
            return true;
        }

        bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            /*
             * Credit: Nogrod (HumanNPC)
             */
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            var input = player.serverInput;
            if (input.current == null) return false;

            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }
        #endregion

        #region Data
        /*
         * This class handles the in-memory state of a player.
         */
        public class PlayerState
        {
            static readonly Core.SQLite.Libraries.SQLite sqlite = Interface.Oxide.GetLibrary<Core.SQLite.Libraries.SQLite>();
            static Core.Database.Connection sqlConnection;
            BasePlayer player;
            int id;
            int plagueLevel;
            int kinChangesCount;
            bool pristine;
            Dictionary<ulong, Association> associations = new Dictionary<ulong, Association>();
            Dictionary<ulong, Kin> kins = new Dictionary<ulong, Kin>();
            List<ulong> kinRequests = new List<ulong>();

            const string UpdateAssociation = "UPDATE associations SET level=@0 WHERE associations.id = @1;";
            const string InsertAssociation = "INSERT INTO associations (player_id,associate_id,level) VALUES (@0,@1,@2);";
            const string CheckAssociationExists = "SELECT id FROM associations WHERE player_id == @0 AND associate_id == @1;";
            const string DeleteAssociation = "DELETE FROM associations WHERE id=@0";
            const string InsertPlayer = "INSERT OR IGNORE INTO players (user_id, name, plague_level, kin_changes_count, pristine) VALUES (@0, @1,0,0,1);";
            const string SelectPlayer = "SELECT * FROM players WHERE players.user_id == @0;";
            const string UpdatePlayerPlagueLevel = "UPDATE players SET plague_level=@0,pristine=@1 WHERE players.user_id == @2;";
            const string SelectAssociations = @"
                SELECT associations.id, associations.player_id, associations.associate_id, associations.level, players.user_id, players.name
                FROM associations
                JOIN players ON associations.associate_id = players.id
                WHERE associations.player_id = @0
            ";
            const string SelectKinList = @"
                SELECT kin.self_id, kin.kin_id, players.name as kin_name, players.user_id as kin_user_id
                FROM kin
                JOIN players ON kin.kin_id = players.id
                WHERE kin.self_id = @0
            ";
            const string InsertKin = "INSERT INTO kin (self_id,kin_id) VALUES (@0,@1);";
            const string DeleteKin = "DELETE FROM kin WHERE self_id=@0 AND kin_id=@1";
            const string SelectKinRequestList = @"";

            /*
             * Retrieves a player from database and restore its store or creates a new database entry
             */
            public PlayerState(BasePlayer newPlayer, Func<PlayerState, bool> callback)
            {
                player = newPlayer;
                Interface.Oxide.LogInfo("Loading player: " + player.displayName);

                var sql = new Core.Database.Sql();
                sql.Append(InsertPlayer, player.userID, player.displayName);
                sqlite.Insert(sql, sqlConnection, create_results =>
                {
                    if (create_results == 1) Interface.Oxide.LogInfo("New user created!");

                    sql = new Core.Database.Sql();
                    sql.Append(SelectPlayer, player.userID);

                    sqlite.Query(sql, sqlConnection, results =>
                    {
                        if (results == null) return;

                        if (results.Count > 0)
                        {
                            foreach (var entry in results)
                            {
                                id = Convert.ToInt32(entry["id"]);
                                plagueLevel = Convert.ToInt32(entry["plague_level"]);
                                kinChangesCount = Convert.ToInt32(entry["kin_changes_count"]);
                                pristine = Convert.ToBoolean(entry["pristine"]);
                                break;
                            }
                        }
                        else
                        {
                            Interface.Oxide.LogInfo("Something wrong has happened: Could not find the player with the given user_id!");
                        }

                        associations = new Dictionary<ulong, Association>();
                        kins = new Dictionary<ulong, Kin>();
                        kinRequests = new List<ulong>();

                        loadAssociations();
                        loadKinList();
                        //loadKinRequestList();
                        callback?.Invoke(this);
                    });
                });
            }

            public static void setupDatabase(RustPlugin plugin)
            {
                sqlConnection = sqlite.OpenDb($"Plagued.db", plugin);

                var sql = new Core.Database.Sql();

                sql.Append(@"CREATE TABLE IF NOT EXISTS players (
                                 id INTEGER PRIMARY KEY   AUTOINCREMENT,
                                 user_id TEXT UNIQUE NOT NULL,
                                 name TEXT,
                                 plague_level INTEGER,
                                 kin_changes_count INTEGER,
                                 pristine INTEGER
                               );");

                sql.Append(@"CREATE TABLE IF NOT EXISTS associations (
                                id INTEGER PRIMARY KEY   AUTOINCREMENT,
                                player_id integer NOT NULL,
                                associate_id integer NOT NULL,
                                level INTEGER,
                                FOREIGN KEY (player_id) REFERENCES players(id),
                                FOREIGN KEY (associate_id) REFERENCES players(id)
                            );");

                sql.Append(@"CREATE TABLE IF NOT EXISTS kin (
                                self_id integer NOT NULL,
                                kin_id integer NOT NULL,
                                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                                FOREIGN KEY (self_id) REFERENCES players(id),
                                FOREIGN KEY (kin_id) REFERENCES players(id),
                                PRIMARY KEY (self_id,kin_id)
                            );");

                sql.Append(@"CREATE TABLE IF NOT EXISTS kin_request (
                                requester_id integer NOT NULL,
                                target_id integer NOT NULL,
                                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                                FOREIGN KEY (requester_id) REFERENCES players(id),
                                FOREIGN KEY (target_id) REFERENCES players(id),
                                PRIMARY KEY (requester_id,target_id)
                            );");

                sqlite.Insert(sql, sqlConnection);
            }

            public static void closeDatabase()
            {
                sqlite.CloseDb(sqlConnection);
            }

            /*
             * Increases the affinity of an associate and returns his new affinity
             */
            Association increaseAssociateAffinity(BasePlayer associate)
            {
                if (associate == null) return null;
                if (player.userID == associate.userID) return null;
                if (disableSleeperAffinity && !BasePlayer.activePlayerList.Contains(associate)) return null;

                Association association = null;

                if (associations.ContainsKey(associate.userID))
                {
                    association = associations[associate.userID];
                    if ((association.level + affinityIncRate) < int.MaxValue) association.level += affinityIncRate;
                }
                else
                {
                    createAssociation(associate.userID, associationRef =>
                    {
                        if (associationRef != null)
                        {
                            association = associationRef;
                            associations.Add(associate.userID, associationRef);
                        }

                        return true;
                    });
                }
//#if DEBUG
//                Plugin.Puts(player.displayName + " -> " + associate.displayName + " = " + associations[associate.userID].ToString());
//#endif
                return association;
            }

            /*
             * Increases the affinity of all the associations in the list and increases the plague penalty if some associations are over the plague threshold
             * It also decreases the plague treshold if all the associates are kin or under the threshold
             */
            public void increasePlaguePenalty(List<BasePlayer> associates)
            {
                int contagionVectorsCount = 0;
                var sql = new Core.Database.Sql();

                foreach (BasePlayer associate in associates)
                {
                    if (isKinByUserID(associate.userID)) continue;

                    Association association = increaseAssociateAffinity(associate);

                    if (association == null) continue;

                    sql.Append(UpdateAssociation, association.level, association.id);

                    if (association.level >= plagueMinAffinity)
                    {
                        contagionVectorsCount++;
                    }
                }

                sqlite.Update(sql, sqlConnection);

                if (contagionVectorsCount > 0)
                {
                    increasePlagueLevel(contagionVectorsCount);
                }
                else
                {
                    decreasePlagueLevel();
                }
#if DEBUG
                Plugin.Puts(player.displayName + " -> " + plagueLevel);
#endif
            }

            /*
             * Decreases the affinity of all associations and decreases the plague level.
             */
            public void decreasePlaguePenalty()
            {
                decreaseAssociationsLevel();

                if (!pristine) decreasePlagueLevel();
            }

            public void increasePlagueLevel(int contagionVectorCount)
            {
                if ((plagueLevel + (contagionVectorCount * plagueIncreaseRate)) <= 10000)
                {
                    plagueLevel += contagionVectorCount * plagueIncreaseRate;

                    if (pristine == true)
                    {
                        pristine = false;
                        MsgPlayer(player, Plugin.lang.GetMessage("playermsg1", Plugin, player?.UserIDString));
#if DEBUG
                        Plugin.Puts(player.displayName + " is now sick.");
#endif
                    }

                    syncPlagueLevel();
                }
#if DEBUG
                Plugin.Puts(player.displayName + "'s new plague level: " + plagueLevel.ToString());
#endif
            }

            public void decreasePlagueLevel()
            {
                if ((plagueLevel - plagueDecreaseRate) >= 0)
                {
                    plagueLevel -= plagueDecreaseRate;

                    if (plagueLevel == 0)
                    {
                        pristine = true;
                        MsgPlayer(player, Plugin.lang.GetMessage("playermsg2", Plugin, player?.UserIDString));
#if DEBUG
                        Plugin.Puts(player.displayName + " is now cured.");
#endif
                    }

                    syncPlagueLevel();
                }
            }

            public void decreaseAssociationsLevel()
            {
                if (associations.Count == 0) return;

                List<ulong> to_remove = new List<ulong>();
                var sql = new Core.Database.Sql();

                foreach (ulong key in associations.Keys)
                {
                    Association association = associations[key];
                    int new_affinity = association.level - affinityDecRate;
                    if (new_affinity >= 1)
                    {
                        association.level = association.level - affinityDecRate;
                        sql.Append(UpdateAssociation, association.level, association.id);
                    }
                    else if (new_affinity <= 0)
                    {
                        sql.Append(DeleteAssociation, association.id);
                        to_remove.Add(key);
                    }
                }

                foreach (ulong keyToRemove in to_remove)
                {
                    associations.Remove(keyToRemove);
                }

                sqlite.ExecuteNonQuery(sql, sqlConnection);
            }

            public bool isKinByUserID(ulong userID)
            {
                foreach (var item in kins)
                {
                    if (item.Value.kin_user_id == userID)
                    {
                        return true;
                    }
                    if(friendsAutoKin && Plugin.IsFriend(userID, player.userID))
                    {
#if DEBUG
                        Plugin.Puts($"UserID {userID.ToString()} is a friend of {player.userID}");
#endif
                        addKin(userID);
                        return true;
                    }
                }

                return false;
            }

            public bool hasKinRequest(ulong kinID) => kinRequests.Contains(kinID);

            public bool addKinRequest(ulong kinID)
            {
                if (!kinRequests.Contains(kinID))
                {
                    kinRequests.Add(kinID);

                    return true;
                }

                return false;
            }

            public bool addKin(ulong kinUserID)
            {
                if (kins.Count + 1 <= maxKin && !isKinByUserID(kinUserID))
                {
                    if (kinRequests.Contains(kinUserID)) kinRequests.Remove(kinUserID);
                    Kin newKin = createKin(kinUserID);
                    newKin.kin_user_id = kinUserID;
                    kins.Add(kinUserID, newKin);

                    return true;
                }

                return false;
            }

            public bool removeKinById(int id)
            {
                if ((kinChangesCount + 1) <= maxKinChanges)
                {
                    foreach (Kin kin in kins.Values)
                        if (kin.kin_id == id)
                            return forceRemoveKin(kin.kin_user_id);
                }

                return false;
            }

            public bool removeKin(ulong kinUserID)
            {
                if ((kinChangesCount + 1) <= maxKinChanges)
                    return forceRemoveKin(kinUserID);

                return false;
            }

            public bool forceRemoveKin(ulong kinUserID)
            {
                if (isKinByUserID(kinUserID))
                {
                    kinChangesCount++;
                    Kin kin = kins[kinUserID];

                    var sql = new Core.Database.Sql();
                    sql.Append(DeleteKin, kin.self_id, kin.kin_id);
                    sqlite.ExecuteNonQuery(sql, sqlConnection);

                    kins.Remove(kinUserID);

                    return true;
                }

                return false;
            }

            public List<string> getKinList()
            {
                List<string> kinList = new List<string>();

                foreach (Kin kin in kins.Values)
                {
                    kinList.Add(String.Format("{0} (Id: {1})", kin.kin_name, kin.kin_id));
                }

                return kinList;
            }

            public List<string> getAssociatesList()
            {
                List<string> associatesList = new List<string>();

                foreach (Association association in associations.Values)
                    associatesList.Add(string.Format("{0} (Id: {1} | Level: {2})", association.associate_name, association.associate_id, association.getAffinityLabel()));

                return associatesList;
            }

            public int getPlagueLevel() => plagueLevel;

            public int getId() => id;

            public bool getPristine() => pristine;

            Kin createKin(ulong kinUserId)
            {
                Kin kin = new Kin(id);

                var sql = new Core.Database.Sql();
                sql.Append(SelectPlayer, kinUserId);

                sqlite.Query(sql, sqlConnection, list =>
                {
                    if (list == null) return;

                    foreach (var user in list)
                    {
                        kin.kin_id = Convert.ToInt32(user["id"]);
                        kin.kin_name = Convert.ToString(user["name"]);
                        kin.kin_user_id = kinUserId;
                        break;
                    }

                    kin.create();
                });

                return kin;
            }

            void createAssociation(ulong associate_user_id, Func<Association, bool> callback)
            {
                Association association = new Association();

                var sql = new Core.Database.Sql();
                sql.Append(SelectPlayer, associate_user_id);
                sqlite.Query(sql, sqlConnection, list =>
                {
                    if (list == null) return;
                    if (list.Count == 0)
                    {
                        callback(null);
                        return;
                    };

                    foreach (var user in list)
                    {
                        association.player_id = id;
                        association.associate_id = Convert.ToInt32(user["id"]);
                        association.associate_user_id = associate_user_id;
                        association.associate_name = Convert.ToString(user["name"]);
                        association.level = 0;
                        break;
                    }

                    association.create();
                    callback(association);
                });
            }

            void syncPlagueLevel()
            {
                var sql = new Core.Database.Sql();
                sql.Append(UpdatePlayerPlagueLevel, plagueLevel, (pristine ? 1 : 0), player.userID);
                sqlite.Update(sql, sqlConnection);
            }

            void loadAssociations()
            {
                var sql = new Core.Database.Sql();
                sql.Append(SelectAssociations, id);
                sqlite.Query(sql, sqlConnection, results =>
                {
                    if (results == null) return;

                    foreach (var association_result in results)
                    {
                        Association association = new Association();
                        association.load(association_result);
                        associations[association.associate_user_id] = association;
                    }
                });
            }

            void loadKinList()
            {
                var sql = new Core.Database.Sql();
                sql.Append(SelectKinList, id);
                sqlite.Query(sql, sqlConnection, results =>
                {
                    if (results == null) return;

                    foreach (var kinResult in results)
                    {
                        Kin kin = new Kin(id);
                        kin.load(kinResult);
                        kins[kin.kin_user_id] = kin;
                    }
                });
            }

            void loadKinRequestList()
            {
                var sql = new Core.Database.Sql();
                sql.Append(SelectKinRequestList, id);
                sqlite.Query(sql, sqlConnection, results =>
                {
                    if (results == null) return;

                    foreach (var kinRequest in results)
                    {
                        kinRequests.Add((ulong)Convert.ToInt64(kinRequest["user_id"]));
                    }
                });
            }

            class Association
            {
                public int id;
                public int player_id;
                public int associate_id;
                public ulong associate_user_id;
                public string associate_name;
                public int level;

                public void create()
                {
                    var sql = new Core.Database.Sql();
                    sql.Append(CheckAssociationExists, player_id, associate_id);

                    // Check if the relationship exists before creating it
                    sqlite.Query(sql, sqlConnection, check_results =>
                    {
                        if (check_results.Count > 0) return;

                        sql = new Core.Database.Sql();

                        sql.Append(InsertAssociation, player_id, associate_id, level);
                        sqlite.Insert(sql, sqlConnection, result =>
                        {
                            if (result == 0) return;
                            id = (int)sqlConnection.LastInsertRowId;
                        });
                    });
                }

                public void load(Dictionary<string, object> association)
                {
                    id = Convert.ToInt32(association["id"]);
                    associate_name = Convert.ToString(association["name"]);
                    associate_user_id = (ulong)Convert.ToInt64(association["user_id"]);
                    associate_id = Convert.ToInt32(association["associate_id"]);
                    player_id = Convert.ToInt32(association["player_id"]);
                    level = Convert.ToInt32(association["level"]);
                }

                public string getAffinityLabel()
                {
                    if (level >= plagueMinAffinity)
                    {
                        return "Associate";
                    }
                    else
                    {
                        return "Acquaintance";
                    }
                }
            }

            class Kin
            {
                public int self_id;
                public int kin_id;
                public ulong kin_user_id;
                public string kin_name;
                public int player_one_id;
                public int player_two_id;

//                Kin() { }

                public Kin(int p_self_id)
                {
                    self_id = p_self_id;
                }

                public void create()
                {
                    var sql = new Core.Database.Sql();
                    sql.Append(InsertKin, self_id, kin_id);
                    sqlite.Insert(sql, sqlConnection);
                }

                public void load(Dictionary<string, object> kin)
                {
                    self_id = Convert.ToInt32(kin["self_id"]);
                    kin_id = Convert.ToInt32(kin["kin_id"]);
                    kin_name = Convert.ToString(kin["kin_name"]);
                    kin_user_id = (ulong)Convert.ToInt64(kin["kin_user_id"]);
                }
            }
        }
        #endregion

        #region Unity Components
        /*
         * This component adds a timers and collects all players colliders in a given radius. It then triggers custom hooks to reflect the situation of a given player
         */
        public class ProximityDetector : MonoBehaviour
        {
            public BasePlayer player;

            public void DisableProximityCheck() => CancelInvoke("CheckProximity");

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckProximity", 0, 2.5f);
            }

            void OnDestroy() => DisableProximityCheck();

            void CheckProximity()
            {
#if DEBUG
                Plugin.Puts($"Checking proximity to {player.displayName}");
#endif
                List<BasePlayer> pNear = new List<BasePlayer>();
                List<BasePlayer> playersNear = new List<BasePlayer>();
                Vis.Entities(player.transform.position, plagueRange, pNear, playerLayer);

                foreach (BasePlayer pl in pNear)
                {
                    if (pl.userID.IsSteamId() && pl.userID != player.userID)
                    {
                        playersNear.Add(pl);
                    }
                }
#if DEBUG
                Plugin.Puts($"Found {playersNear.Count} players within {plagueRange}m range.");
#endif
                if (playersNear.Count > 0)
                {
                    NotifyPlayerProximity(playersNear);
                }
                else
                {
                    NotifyPlayerAlone();
                }
            }

            void NotifyPlayerProximity(List<BasePlayer> players) => Interface.Oxide.CallHook("OnPlayerProximity", player, players);
            void NotifyPlayerAlone() => Interface.Oxide.CallHook("OnPlayerAlone", player);
        }
        #endregion
    }
}
