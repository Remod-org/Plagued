using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Plagued", "Psi|ocybin/RFC1920", "0.3.8", ResourceId = 1991)]
    [Description("Everyone is infected")]
    internal class Plagued : RustPlugin
    {
        #region Initialization
        [PluginReference]
        private readonly Plugin Friends, Clans, Vanish;

        private ConfigData configData;
        public static Plagued Plugin;

        private static int playerLayer;
        private Dictionary<ulong, PlayerState> playerStates = new Dictionary<ulong, PlayerState>();
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region Hooks
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "HELP0", "Welcome to Plagued mod. Try the <color=#81F781>/plagued</color> command for more information." },
                { "HELP1", "<color=#81F781>/plagued addkin</color> => <color=#D8D8D8> Add the player you are looking at to your kin list.</color>" },
                { "HELP2", "<color=#81F781>/plagued delkin</color> => <color=#D8D8D8> Remove the player you are looking at from your kin list.</color>" },
                { "HELP3", "<color=#81F781>/plagued delkin</color> <color=#F2F5A9> number </color> => <color=#D8D8D8> Remove a player from your kin list by kin number.</color>" },
                { "HELP4", "<color=#81F781>/plagued lskin</color> => <color=#D8D8D8> Display your kin list.</color>" },
                { "HELP5", "<color=#81F781>/plagued lsassociates</color> => <color=#D8D8D8> Display your associates list.</color>" },
                { "HELP6", "<color=#81F781>/plagued info</color> => <color=#D8D8D8> Display information about the workings of this mod.</color>" },
                { "INFO1", " ===== Plagued mod ======" },
                { "INFO2", "COVID 19 has decimated most of the population.  You find yourself on a deserted island, lucky to be among the few survivors. But the biological apocalypse is far from being over.  It seems that the virus starts to express itself when certain hormonal changes are triggered by highly social behaviors. It has been noted that small groups of survivors seem to be relatively unaffected, but there isn't one single town or clan that wasn't decimated." },
                { "INFO3", "Workings:\n The longer you hang around others, the sicker you'll get.  However, your kin are unaffected.  Add your friends as kin and you will be able to collaborate.  Choose your kin wisely - There are no big families in this world." },
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
                { "playermsg1", "I don't feel well." },
                { "playermsg2", "I feel much better now." }
            }, this, "en");
        }

        private void Init()
        {
            AddCovalenceCommand("plagued", "CmdPlagued");
            PlayerState.setupDatabase(this);
        }

        private void OnServerInitialized()
        {
            LoadConfigVariables();
            Plugin = this; // For debug and lang()
            // Set the layer that will be used in the radius search. We only want human players in this case.
            playerLayer = LayerMask.GetMask("Player (Server)");

            // Reload the player states
            playerStates = new Dictionary<ulong, PlayerState>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DoLog($"Adding plague state to {player.displayName}");
                playerStates.Add(player.userID, new PlayerState(player, null));
            }
        }

        private void Loaded()
        {
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

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.allPlayerList)
            {
                DoLog($"Trying to remove proximity detector from {player.displayName}.");
                ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
                if (proximityDetector != null)
                {
                    DoLog($"Removing proximity detector from {player.displayName}.");
                    UnityEngine.Object.Destroy(proximityDetector);
                }
            }
            PlayerState.closeDatabase();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            // Add the player to the player state list
            if (!playerStates.ContainsKey(player.userID))
            {
                // The player was loaded in the current game session
                playerStates.Add(player.userID, new PlayerState(player, null));
                Message(player.IPlayer, "HELP0");
                DoLog(player.displayName + " has been plagued!");
            }
            // Add the proximity detector to the player
            player.gameObject.AddComponent<ProximityDetector>();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            ProximityDetector proximityDetector = player.gameObject.GetComponent<ProximityDetector>();
            if (proximityDetector != null)
            {
                DoLog($"Removing proximity detector from {player.displayName}.");
                UnityEngine.Object.Destroy(proximityDetector);
            }
            DoLog(player.displayName + " is no longer watched!");
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
            const float defaultHealthGain = 0.03f;
            const float defaultCaloriesLoss = 0.05f;
            const float defaultHydrationLoss = 0.025f;
            DoLog("Infection stage " + (plagueLevel / 1000).ToString());
            //if (plagueLevel == 0) return;

            if (plagueLevel <= 1)
            {
                return;
            }
            //DoLog("Infection stage 1 " + player.displayName + " " + player.userID);
            metabolism.pending_health.value += (defaultHealthGain / 2f);

            if (plagueLevel <= 1000)
            {
                return;
            }
            //DoLog("Infection stage 2");
            metabolism.calories.value -= ((defaultCaloriesLoss * 3f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 2000)
            {
                return;
            }
            //DoLog("Infection stage 3");
            metabolism.hydration.value -= ((defaultHydrationLoss * 3f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 3000)
            {
                return;
            }

            metabolism.pending_health.value -= (defaultHealthGain / 2f);

            if (plagueLevel <= 4000)
            {
                return;
            }
            //DoLog("Infection stage 5");
            metabolism.comfort.value = -1;

            if (plagueLevel <= 5000)
            {
                return;
            }
            //DoLog("Infection stage 6");
            metabolism.calories.value -= ((defaultCaloriesLoss * 5f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 6000)
            {
                return;
            }
            //DoLog("Infection stage 7");
            metabolism.hydration.value -= ((defaultHydrationLoss * 5f) + (metabolism.heartrate.value / 10f));

            if (plagueLevel <= 7000)
            {
                return;
            }

            //DoLog("Infection stage 8");
            metabolism.temperature.value -= 0.05f;

            if (plagueLevel <= 8000)
            {
                return;
            }
            //DoLog("Infection stage 9");
            metabolism.bleeding.value += 0.2f;
            metabolism.radiation_poison.value++;

            if (plagueLevel < 10000)
            {
                return;
            }
            //DoLog("Infection stage 10");
            metabolism.poison.value += 1.5f;
            metabolism.radiation_level.value += 1.5f;
        }

        // OUR HOOKS
        private void OnPlayerProximity(BasePlayer player, List<BasePlayer> players)
        {
            if (playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID].increasePlaguePenalty(players);
                DoLog($"{player.displayName} is close to {players.Count - 1} other players!");
                foreach (BasePlayer pl in players)
                {
                    DoLog(pl.displayName);
                }
            }
        }

        private void OnPlayerAlone(BasePlayer player)
        {
            if (playerStates.ContainsKey(player.userID))
            {
                playerStates[player.userID].decreasePlaguePenalty();
                DoLog($"OnPlayerAlone: {player.userID}");
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

        private void cmdAddKin(IPlayer iplayer)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            BasePlayer targetPlayer;

            if (getPlayerLookedAt(player, out targetPlayer))
            {
                DoLog($"Looking at player {targetPlayer.userID}");
                PlayerState state = playerStates[player.userID];
                PlayerState targetPlayerState;
                playerStates.TryGetValue(targetPlayer.userID, out targetPlayerState);

                if (state.isKinByUserID(targetPlayer.userID))
                {
                    Message(iplayer, "already", targetPlayer.displayName);
                    return;
                }
                DoLog($"Trying to add player {targetPlayer.userID} to kin...");
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

        private bool cmdDelKin(IPlayer iplayer)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
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

        private bool cmdDelKin(IPlayer iplayer, int id)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            PlayerState state = playerStates[player.userID];

            if (state.removeKinById(id))
            {
                foreach (KeyValuePair<ulong, PlayerState> item in playerStates)
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

        private void cmdListKin(IPlayer iplayer)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            List<string> kinList = playerStates[player.userID].getKinList();

            displayList(iplayer, "Kin", kinList);
        }

        private void cmdListAssociates(IPlayer iplayer)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            List<string> associatesList = playerStates[player.userID].getAssociatesList();
            displayList(iplayer, "Associates", associatesList);
        }

        private bool cmdInfo(IPlayer iplayer)
        {
            Message(iplayer, "INFO1");
            Message(iplayer, "INFO2");
            Message(iplayer, "INFO3");
            Message(iplayer, "INFO4", configData.maxKin.ToString(), configData.maxKinChanges.ToString());
            return false;
        }
        #endregion

        #region Helpers
        // Send chat message as player
        public static void MsgPlayer(BasePlayer player, string format, params object[] args)
        {
            if (player?.net != null)
            {
                player.SendConsoleCommand("chat.say", (args.Length > 0) ? "/" + string.Format(format, args) : "/" + format, 1f);
            }
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

        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (configData.UseFriends && Friends != null)
            {
                DoLog("Checking Friends...");
                object fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    Puts("  IsFriend: true based on Friends plugin");
                    return true;
                }
            }
            if (configData.UseClans && Clans != null)
            {
                DoLog("Checking Clans...");
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
                    DoLog("  IsFriend: true based on Clans plugin");
                    return true;
                }
            }
            if (configData.UseTeams)
            {
                DoLog("Checking Rust teams...");
                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player.currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (playerTeam == null)
                    {
                        return false;
                    }

                    if (playerTeam.members.Contains(ownerid))
                    {
                        DoLog("  IsFriend: true based on Rust teams");
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Geometry
        private bool getPlayerLookedAt(BasePlayer player, out BasePlayer targetPlayer)
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
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                return false;
            }

            targetPlayer = ((Collider)closestEnt).GetComponentInParent<BasePlayer>();

            if (targetPlayer == null)
            {
                Message(player.IPlayer, "nolook");
                return false;
            }

            return true;
        }

        private bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            /*
             * Credit: Nogrod (HumanNPC)
             */
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);

            RaycastHit[] hits = Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.GetComponentInParent<TriggerBase>() == null && hit.distance < closestdist)
                {
                    closestdist = hit.distance;
                    closestEnt = hit.collider;
                    closestHitpoint = hit.point;
                }
            }

            return !(closestEnt is bool);
        }

        private bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            /*
             * Credit: Nogrod (HumanNPC)
             */
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            InputState input = player.serverInput;
            if (input.current == null)
            {
                return false;
            }

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
            private static readonly Core.SQLite.Libraries.SQLite sqlite = Interface.Oxide.GetLibrary<Core.SQLite.Libraries.SQLite>();
            private static Core.Database.Connection sqlConnection;
            private BasePlayer player;
            private int id;
            private int plagueLevel;
            private int kinChangesCount;
            private bool pristine;
            private Dictionary<ulong, Association> associations = new Dictionary<ulong, Association>();
            private Dictionary<ulong, Kin> kins = new Dictionary<ulong, Kin>();
            private List<ulong> kinRequests = new List<ulong>();

            private const string UpdateAssociation = "UPDATE associations SET level=@0 WHERE associations.id = @1;";
            private const string InsertAssociation = "INSERT INTO associations (player_id,associate_id,level) VALUES (@0,@1,@2);";
            private const string CheckAssociationExists = "SELECT id FROM associations WHERE player_id == @0 AND associate_id == @1;";
            private const string DeleteAssociation = "DELETE FROM associations WHERE id=@0";
            private const string InsertPlayer = "INSERT OR IGNORE INTO players (user_id, name, plague_level, kin_changes_count, pristine) VALUES (@0, @1,0,0,1);";
            private const string SelectPlayer = "SELECT * FROM players WHERE players.user_id == @0;";
            private const string UpdatePlayerPlagueLevel = "UPDATE players SET plague_level=@0,pristine=@1 WHERE players.user_id == @2;";

            private const string SelectAssociations = @"
                SELECT associations.id, associations.player_id, associations.associate_id, associations.level, players.user_id, players.name
                FROM associations
                JOIN players ON associations.associate_id = players.id
                WHERE associations.player_id = @0
            ";

            private const string SelectKinList = @"
                SELECT kin.self_id, kin.kin_id, players.name as kin_name, players.user_id as kin_user_id
                FROM kin
                JOIN players ON kin.kin_id = players.id
                WHERE kin.self_id = @0
            ";

            private const string InsertKin = "INSERT INTO kin (self_id,kin_id) VALUES (@0,@1);";
            private const string DeleteKin = "DELETE FROM kin WHERE self_id=@0 AND kin_id=@1";
            private const string SelectKinRequestList = "";

            /*
             * Retrieves a player from database and restore its store or creates a new database entry
             */
            public PlayerState(BasePlayer newPlayer, Func<PlayerState, bool> callback)
            {
                player = newPlayer;
                Interface.Oxide.LogInfo("Loading player: " + player.displayName);

                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(InsertPlayer, player.userID, player.displayName);
                sqlite.Insert(sql, sqlConnection, create_results =>
                {
                    if (create_results == 1)
                    {
                        Interface.Oxide.LogInfo("New user created!");
                    }

                    sql = new Core.Database.Sql();
                    sql.Append(SelectPlayer, player.userID);

                    sqlite.Query(sql, sqlConnection, results =>
                    {
                        if (results == null)
                        {
                            return;
                        }

                        if (results.Count > 0)
                        {
                            foreach (Dictionary<string, object> entry in results)
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

                Core.Database.Sql sql = new Core.Database.Sql();

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
            private Association increaseAssociateAffinity(BasePlayer associate)
            {
                if (associate == null)
                {
                    return null;
                }

                if (player.userID == associate.userID)
                {
                    return null;
                }

                if (Plugin.configData.disableSleeperAffinity && !BasePlayer.activePlayerList.Contains(associate))
                {
                    return null;
                }

                Association association = null;

                if (associations.ContainsKey(associate.userID))
                {
                    association = associations[associate.userID];
                    if ((association.level + Plugin.configData.affinityIncRate) < int.MaxValue)
                    {
                        association.level += Plugin.configData.affinityIncRate;
                    }
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
                //if (Plugin.configData.debug) Plugin.Puts(player.displayName + " -> " + associate.displayName + " = " + associations[associate.userID].ToString());
                return association;
            }

            /*
             * Increases the affinity of all the associations in the list and increases the plague penalty if some associations are over the plague threshold
             * It also decreases the plague treshold if all the associates are kin or under the threshold
             */
            public void increasePlaguePenalty(List<BasePlayer> associates)
            {
                int contagionVectorsCount = 0;
                Core.Database.Sql sql = new Core.Database.Sql();

                foreach (BasePlayer associate in associates)
                {
                    if (isKinByUserID(associate.userID))
                    {
                        continue;
                    }

                    Association association = increaseAssociateAffinity(associate);

                    if (association == null)
                    {
                        continue;
                    }

                    sql.Append(UpdateAssociation, association.level, association.id);

                    if (association.level >= Plugin.configData.plagueMinAffinity)
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
                if (Plugin.configData.debug) Plugin.Puts(player.displayName + " -> " + plagueLevel);
            }

            /*
             * Decreases the affinity of all associations and decreases the plague level.
             */
            public void decreasePlaguePenalty()
            {
                decreaseAssociationsLevel();

                if (!pristine)
                {
                    decreasePlagueLevel();
                }
            }

            public void increasePlagueLevel(int contagionVectorCount)
            {
                if ((plagueLevel + (contagionVectorCount * Plugin.configData.plagueIncreaseRate)) <= 10000)
                {
                    plagueLevel += contagionVectorCount * Plugin.configData.plagueIncreaseRate;

                    if (pristine)
                    {
                        pristine = false;
                        MsgPlayer(player, Plugin.lang.GetMessage("playermsg1", Plugin, player?.UserIDString));
                        if (Plugin.configData.debug) Plugin.Puts(player.displayName + " is now sick.");
                    }

                    syncPlagueLevel();
                }
                if (Plugin.configData.debug) Plugin.Puts(player.displayName + "'s new plague level: " + plagueLevel.ToString());
            }

            public void decreasePlagueLevel()
            {
                if ((plagueLevel - Plugin.configData.plagueDecreaseRate) >= 0)
                {
                    plagueLevel -= Plugin.configData.plagueDecreaseRate;

                    if (plagueLevel == 0)
                    {
                        pristine = true;
                        MsgPlayer(player, Plugin.lang.GetMessage("playermsg2", Plugin, player?.UserIDString));
                        if (Plugin.configData.debug) Plugin.Puts(player.displayName + " is now cured.");
                    }

                    syncPlagueLevel();
                }
            }

            public void decreaseAssociationsLevel()
            {
                if (associations.Count == 0)
                {
                    return;
                }

                List<ulong> to_remove = new List<ulong>();
                Core.Database.Sql sql = new Core.Database.Sql();

                foreach (ulong key in associations.Keys)
                {
                    Association association = associations[key];
                    int new_affinity = association.level - Plugin.configData.affinityDecRate;
                    if (new_affinity >= 1)
                    {
                        association.level -= Plugin.configData.affinityDecRate;
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
                foreach (KeyValuePair<ulong, Kin> item in kins)
                {
                    if (item.Value.kin_user_id == userID)
                    {
                        return true;
                    }
                    if (Plugin.configData.friendsAutoKin && Plugin.IsFriend(userID, player.userID))
                    {
                        if (Plugin.configData.debug) Plugin.Puts($"UserID {userID.ToString()} is a friend of {player.userID}");
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
                if (kins.Count + 1 <= Plugin.configData.maxKin && !isKinByUserID(kinUserID))
                {
                    if (kinRequests.Contains(kinUserID))
                    {
                        kinRequests.Remove(kinUserID);
                    }

                    Kin newKin = createKin(kinUserID);
                    newKin.kin_user_id = kinUserID;
                    kins.Add(kinUserID, newKin);

                    return true;
                }

                return false;
            }

            public bool removeKinById(int id)
            {
                if ((kinChangesCount + 1) <= Plugin.configData.maxKinChanges)
                {
                    foreach (Kin kin in kins.Values)
                    {
                        if (kin.kin_id == id)
                        {
                            return forceRemoveKin(kin.kin_user_id);
                        }
                    }
                }

                return false;
            }

            public bool removeKin(ulong kinUserID)
            {
                if ((kinChangesCount + 1) <= Plugin.configData.maxKinChanges)
                {
                    return forceRemoveKin(kinUserID);
                }

                return false;
            }

            public bool forceRemoveKin(ulong kinUserID)
            {
                if (isKinByUserID(kinUserID))
                {
                    kinChangesCount++;
                    Kin kin = kins[kinUserID];

                    Core.Database.Sql sql = new Core.Database.Sql();
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
                {
                    associatesList.Add(string.Format("{0} (Id: {1} | Level: {2})", association.associate_name, association.associate_id, association.getAffinityLabel()));
                }

                return associatesList;
            }

            public int getPlagueLevel() => plagueLevel;

            public int getId() => id;

            public bool getPristine() => pristine;

            private Kin createKin(ulong kinUserId)
            {
                Kin kin = new Kin(id);

                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(SelectPlayer, kinUserId);

                sqlite.Query(sql, sqlConnection, list =>
                {
                    if (list == null)
                    {
                        return;
                    }

                    foreach (Dictionary<string, object> user in list)
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

            private void createAssociation(ulong associate_user_id, Func<Association, bool> callback)
            {
                Association association = new Association();

                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(SelectPlayer, associate_user_id);
                sqlite.Query(sql, sqlConnection, list =>
                {
                    if (list == null)
                    {
                        return;
                    }

                    if (list.Count == 0)
                    {
                        callback(null);
                        return;
                    }

                    foreach (Dictionary<string, object> user in list)
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

            private void syncPlagueLevel()
            {
                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(UpdatePlayerPlagueLevel, plagueLevel, pristine ? 1 : 0, player.userID);
                sqlite.Update(sql, sqlConnection);
            }

            private void loadAssociations()
            {
                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(SelectAssociations, id);
                sqlite.Query(sql, sqlConnection, results =>
                {
                    if (results == null)
                    {
                        return;
                    }

                    foreach (Dictionary<string, object> association_result in results)
                    {
                        Association association = new Association();
                        association.load(association_result);
                        associations[association.associate_user_id] = association;
                    }
                });
            }

            private void loadKinList()
            {
                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(SelectKinList, id);
                sqlite.Query(sql, sqlConnection, results =>
                {
                    if (results == null)
                    {
                        return;
                    }

                    foreach (Dictionary<string, object> kinResult in results)
                    {
                        Kin kin = new Kin(id);
                        kin.load(kinResult);
                        kins[kin.kin_user_id] = kin;
                    }
                });
            }

            private void loadKinRequestList()
            {
                Core.Database.Sql sql = new Core.Database.Sql();
                sql.Append(SelectKinRequestList, id);
                sqlite.Query(sql, sqlConnection, results =>
                {
                    if (results == null)
                    {
                        return;
                    }

                    foreach (Dictionary<string, object> kinRequest in results)
                    {
                        kinRequests.Add((ulong)Convert.ToInt64(kinRequest["user_id"]));
                    }
                });
            }

            private class Association
            {
                public int id;
                public int player_id;
                public int associate_id;
                public ulong associate_user_id;
                public string associate_name;
                public int level;

                public void create()
                {
                    Core.Database.Sql sql = new Core.Database.Sql();
                    sql.Append(CheckAssociationExists, player_id, associate_id);

                    // Check if the relationship exists before creating it
                    sqlite.Query(sql, sqlConnection, check_results =>
                    {
                        if (check_results.Count > 0)
                        {
                            return;
                        }

                        sql = new Core.Database.Sql();

                        sql.Append(InsertAssociation, player_id, associate_id, level);
                        sqlite.Insert(sql, sqlConnection, result =>
                        {
                            if (result == 0)
                            {
                                return;
                            }

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
                    if (level >= Plugin.configData.plagueMinAffinity)
                    {
                        return "Associate";
                    }
                    else
                    {
                        return "Acquaintance";
                    }
                }
            }

            private class Kin
            {
                public int self_id;
                public int kin_id;
                public ulong kin_user_id;
                public string kin_name;
                public int player_one_id;
                public int player_two_id;

                //Kin() { }

                public Kin(int p_self_id)
                {
                    self_id = p_self_id;
                }

                public void create()
                {
                    Core.Database.Sql sql = new Core.Database.Sql();
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

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckProximity", 0, 2.5f);
            }

            private void OnDestroy() => DisableProximityCheck();

            private void CheckProximity()
            {
                if (Plugin.configData.debug) Plugin.Puts($"Checking proximity to {player.displayName}");
                List<BasePlayer> pNear = new List<BasePlayer>();
                Vis.Entities(player.transform.position, Plugin.configData.plagueRange, pNear, playerLayer);

                List<BasePlayer> playersNear = new List<BasePlayer>();
                foreach (BasePlayer pl in pNear)
                {
                    if (pl.userID.IsSteamId() && pl.userID != player.userID && !pl.IsSleeping() && pl.IsAlive())
                    {
                        if (Plugin.Vanish != null && (bool)Plugin.Vanish?.CallHook("IsInvisible", pl))
                        {
                            continue;
                        }
                        playersNear.Add(pl);
                    }
                }
                if (Plugin.configData.debug) Plugin.Puts($"Found {playersNear.Count} players within {Plugin.configData.plagueRange}m range.");
                if (playersNear.Count > 0)
                {
                    NotifyPlayerProximity(playersNear);
                }
                else
                {
                    NotifyPlayerAlone();
                }
            }

            private void NotifyPlayerProximity(List<BasePlayer> players) => Interface.Oxide.CallHook("OnPlayerProximity", player, players);
            private void NotifyPlayerAlone() => Interface.Oxide.CallHook("OnPlayerAlone", player);
        }
        #endregion

        #region Configuration
        public class ConfigData
        {
            public int plagueRange;
            public int plagueIncreaseRate;
            public int plagueDecreaseRate;
            public int plagueMinAffinity;
            public int affinityIncRate;
            public int affinityDecRate;
            public int maxKin;
            public int maxKinChanges;
            public bool disableSleeperAffinity;
            public bool UseFriends;
            public bool UseClans;
            public bool UseTeams;
            public bool friendsAutoKin;

            public bool debug;
            public VersionNumber Version;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file");
            ConfigData config = new ConfigData
            {
                plagueRange = 20,
                plagueIncreaseRate = 50,
                plagueDecreaseRate = 30,
                plagueMinAffinity = 3000,
                affinityIncRate = 100,
                affinityDecRate = 60,
                maxKin = 2,
                maxKinChanges = 3,
                disableSleeperAffinity = false,
                UseFriends = false,
                UseClans = false,
                UseTeams = false,
                friendsAutoKin = false,
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion Configuration

        private void DoLog(string message, int indent = 0)
        {
            if (!configData.debug) return;
            Debug.LogWarning("".PadLeft(indent, ' ') + "[PhoneBooth] " + message);
        }
    }
}
