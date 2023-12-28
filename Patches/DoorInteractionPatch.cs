using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;

namespace Lethal_Doors.Patches
{
    [HarmonyPatch(typeof(HangarShipDoor))]
    
   
    internal class DoorInteractionPatch
    {

        private static readonly Vector3 doorPosition = new Vector3(-5.72f, 0.305f, -14.1f); // Using hardcoded position of hangar door since Object position is not aligned in world
        private static float doorClosingTimer = -1f; // Timer to track the door closing duration
        private static readonly float doorClosingDuration = 0.30f; // Known duration of closing is .86 which is the animation length
        private static readonly HashSet<int> affectedPlayers = new HashSet<int>(); // Track affected players
        //private static float doorClosedTimer = 0f; // Timer to introduce delay after door is closed

        [HarmonyPostfix]
        [HarmonyPatch(nameof(HangarShipDoor.Update))]
        static void PostfixUpdate(HangarShipDoor __instance)
        {
           
            string DoorStateLog = DoorState(__instance.shipDoorsAnimator); // return state of door based on animation

            // if door is closing & power is draining start timer from 0f
            if (IsDoorClosing(__instance))
            {
                if (doorClosingTimer < 0f)
                {
                    doorClosingTimer = 0f;

                    // if door is open clear affected players to avoid repeat damage
                    if (!IsDoorClosed(__instance.shipDoorsAnimator))
                    {
                        affectedPlayers.Clear();
                    }
                }

                // if the closing door timer is less than the animation duration & the statis is closed apply impact
                if (doorClosingTimer < doorClosingDuration && IsDoorClosed(__instance.shipDoorsAnimator))
                {
                    CheckForPlayersAndApplyDamage();
                    CheckForMouthDogsAndApplyDamage();


                }
                else
                {
                    affectedPlayers.Clear(); // Clear affected players when door starts opening

                }

                doorClosingTimer += Time.deltaTime; // increment timer
            }
            else
            {
                doorClosingTimer = -1f; // Reset timer when door not closing
            }
        }

        // uses power level of door to determin if door is closed/closing
        // prevents players from killing each other before the ship lands.
        static bool IsDoorClosing(HangarShipDoor door)
        {
            
            bool isPowerDecreasing = door.doorPower < 1f;
            
            //Debug.Log($"[Lethal Doors] Power Decreasing - {isPowerDecreasing}, isButton Enabled: {door.buttonsEnabled}");

            return isPowerDecreasing;
        }

        //Gets instance of players in game & loops through each player to to confirm if they are in danger zone & should be impacted
        static void CheckForPlayersAndApplyDamage()
        {
            Debug.Log("[Lethal Doors] Checking for players to apply damage");

            if (StartOfRound.Instance != null && StartOfRound.Instance.allPlayerScripts != null)
            {
                foreach (var player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (player != null && IsPlayerInDangerZone(player))
                    {
                        int playerID = (int)player.playerClientId;
                        if (!affectedPlayers.Contains(playerID))
                        {
                            Debug.Log($"[Lethal Doors] Player {player.playerUsername} is in danger zone");
                            ApplyLethalDamageOrInjurePlayer(player);
                            affectedPlayers.Add(playerID); // Add player to affected list
                            Debug.Log($"[Lethal Doors] Added to affected list added");

                        }
                    }
                }
            }
        }


        // check if the distance between the player position & door position is less than the threshold of impact.
        // threshold used to tweak sensitivity 
        static bool IsPlayerInDangerZone(PlayerControllerB player)
        {
            float threshold = 1.0f; // Threshold for danger zone
            bool inDangerZone = Vector3.Distance(player.transform.position, doorPosition) < threshold;
            //Debug.Log($"[Lethal Doors] IsPlayerInDangerZone for {player.playerUsername}: {inDangerZone}, Position:{player.transform.position} ");
            return inDangerZone;
        }

        // check if the distance between the enemy position & door position is less than the threshold of impact.
        // larger threshold for weird clipping of mouth dog
        static bool IsEnemyInDangerZone(EnemyAI enemy)
        {
            float threshold = 2.0f; // Threshold for danger zone
            bool inDangerZone = Vector3.Distance(enemy.transform.position, doorPosition) < threshold;
            return inDangerZone;
        }

        // 50/50 chance to either critically injure or kill the player
        static void ApplyLethalDamageOrInjurePlayer(PlayerControllerB player)
        {
            Debug.Log($"[Lethal Doors] {player.playerUsername} in danger zone, Position:{player.transform.position} ");
            Debug.Log($"[Lethal Doors] Applying lethal damange to player {player.playerUsername}");

            // kills player
            if (player.criticallyInjured || UnityEngine.Random.Range(0, 2) == 0)
            {
                //player.MakeCriticallyInjuredServerRpc();
                player.DamagePlayer(110, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
                
                //player.KillPlayer(Vector3.zero, true, CauseOfDeath.Crushing, 0);
                //KillPlayerServerRpc((int)player.playerClientId);
                Debug.Log($"[Lethal Doors] Heads: kill player ClientID:{(int)player.playerClientId} ");
            }
            else //injure player
            {
                Debug.Log($"[Lethal Doors] Tails: injure player ClientID:{(int)player.playerClientId} ");

                player.DamagePlayer(90, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
                player.AddBloodToBody();
                player.MakeCriticallyInjuredServerRpc();
                affectedPlayers.Add((int)player.playerClientId); // Add player to affected list

            }
        }
       
        // returns if true if door is closed based on animation state
        static bool IsDoorClosed(Animator animator)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("ShipDoorClose");
        }

        // returns the animation state names used on the door.
        static string DoorState(Animator animator)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("ShipDoorOpen") ? "ShipDoorOpen" :
                   stateInfo.IsName("ShipDoorClose") ? "ShipDoorClose" :
                   "Unknown";
        }

   
        // loops though all enemies spawned to get their position from the door to also damange them.
        // updated to impact all enemies in game
        static void CheckForMouthDogsAndApplyDamage()
        {
            // Assuming roundManager is already defined and accessible
            RoundManager roundManager = GameObject.FindObjectOfType<RoundManager>();
            if (roundManager != null && roundManager.SpawnedEnemies != null)
            {
                foreach (var enemy in roundManager.SpawnedEnemies)
                {
                    // updated to impact all enemies in game. old statement: if (enemy != null && enemy.gameObject.name == "MouthDog(Clone)")
                    if (enemy != null)
                    {
                        //Debug.Log($"{enemy.name} - In World: {enemy.transform.position}");

                        // Check if the enemy is in the danger zone & kills them without despawning their body
                        if (IsEnemyInDangerZone(enemy))
                        {
                            Debug.Log($"[Lethal Doors] {enemy.name} is in danger zone at position {enemy.transform.position}");
                            enemy.HitEnemy(999);
                            enemy.KillEnemy(false);
                            Debug.Log($"[Lethal Doors] {enemy.name} killed");
                        }
                    }
                }
            }
        }

        /// <summary>
        ///  No Code Below this point is used. Just left if needed in future. 
        /// </summary>

        // not used - test function used to help validate the enemy names & positions
        static void LogEnemyPositions()
        {
            RoundManager roundManager = GameObject.FindObjectOfType<RoundManager>();
            if (roundManager != null && roundManager.SpawnedEnemies != null)
            {
                foreach (var enemy in roundManager.SpawnedEnemies)
                {
                    if (enemy != null && enemy.gameObject.name == "MouthDog(Clone)")
                    {
                        Debug.Log($"{enemy.name}: {enemy.transform.position}");

                    }
                }
            }
        }
        // not used - RPC to initiate the kill on the server
        [ServerRpc(RequireOwnership = false)]
        static void KillPlayerServerRpc(int playerId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                KillPlayerClientRpc(playerId);
            }
        }

        // not used - RPC to execute the kill on the client
        [ClientRpc]
        static void KillPlayerClientRpc(int playerId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
                if (player != null)
                {
                    // Replace these with actual values you want to use
                    player.KillPlayer(Vector3.zero, spawnBody: true, CauseOfDeath.Crushing, 0);
                }
            }
        }


        // leaving old code for tracking animations if I come back to this approach 
        // would be a way to allow players to kill each other before the round begins & ship lands

        static void LogDoorAnimationDuration(HangarShipDoor door)
        {


            if (door == null || door.shipDoorsAnimator == null)
            {
                Debug.LogError("Door component not found!");
                return;
            }

            string animationName = "ShipDoorClose";
            float animationDuration = GetAnimationDuration(door.shipDoorsAnimator, animationName);

            if (animationDuration >= 0)
            {
                Debug.Log($"Duration of {animationName}: {animationDuration} seconds");
            }
            else
            {
                Debug.LogError($"Animation {animationName} not found!");
            }
        }

        static float GetAnimationDuration(Animator animator, string animationName)
        {
            RuntimeAnimatorController ac = animator.runtimeAnimatorController;

            foreach (AnimationClip clip in ac.animationClips)
            {
                if (clip.name == animationName)
                {
                    return clip.length;
                }
            }

            return -1f; // Return -1 if not found
        }

        static void LogAllAnimationNames(HangarShipDoor door)
        {
            if (door == null || door.shipDoorsAnimator == null)
            {
                Debug.LogError("Door or Animator component not found!");
                return;
            }

            RuntimeAnimatorController ac = door.shipDoorsAnimator.runtimeAnimatorController;

            Debug.Log("Logging all animation names:");
            foreach (AnimationClip clip in ac.animationClips)
            {
                Debug.Log(clip.name);
            }
        }


    }
}
