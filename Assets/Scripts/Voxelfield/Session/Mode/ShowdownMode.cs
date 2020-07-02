using System;
using System.Collections.Generic;
using System.Linq;
using Swihoni.Components;
using Swihoni.Sessions;
using Swihoni.Sessions.Components;
using Swihoni.Sessions.Items.Modifiers;
using Swihoni.Sessions.Modes;
using Swihoni.Sessions.Player;
using Swihoni.Sessions.Player.Components;
using Swihoni.Sessions.Player.Modifiers;
using Swihoni.Util.Math;
using UnityEngine;
using Voxel.Map;
using Random = UnityEngine.Random;

namespace Voxelfield.Session.Mode
{
    using QueuedTeamSpawns = IReadOnlyList<Queue<(Position3Int, Container)>>;

    [CreateAssetMenu(fileName = "Showdown", menuName = "Session/Mode/Showdown", order = 0)]
    public class ShowdownMode : DeathmatchModeBase
    {
        // public const uint BuyTimeUs = 15_000_000u, FightTimeUs = 300_000_000u;
        // public const uint BuyTimeUs = 60_000_000u, FightTimeUs = 300_000_000u;
        public const uint BuyTimeUs = 2_000_000u, FightTimeUs = 5_000_000u;
        public const uint SecureTimeUs = 5_000_000;

        private const int TeamCount = 5, PlayersPerTeam = 3, TotalPlayers = TeamCount * PlayersPerTeam;

        [SerializeField] private LayerMask m_ModelMask = default;
        private CurePackageBehavior[] m_CurePackages;
        private readonly RaycastHit[] m_CachedHits = new RaycastHit[1];

        private static ushort GetCost(byte itemId)
        {
            switch (itemId)
            {
                case ItemId.Rifle:
                    return 2000;
                case ItemId.Shotgun:
                    return 1300;
                case ItemId.Sniper:
                    return 5000;
                case ItemId.Deagle:
                    return 700;
                case ItemId.Grenade:
                    return 150;
                case ItemId.Molotov:
                    return 400;
                case ItemId.C4:
                    return 600;
            }
            throw new ArgumentException("Can't buy this item id");
        }

        public override void Modify(SessionBase session, Container container, uint durationUs)
        {
            base.Modify(session, container, durationUs);
            var stage = container.Require<ShowdownSessionComponent>();
            if (stage.number.WithoutValue) // If in warmup
            {
                int playerCount = GetPlayerCount(container);
                if (playerCount == 1)
                    // if (playerCount == TotalPlayers)
                {
                    StartFirstStage(session, container, stage);
                }
            }
            if (stage.number.WithValue)
            {
                if (stage.remainingUs > durationUs) stage.remainingUs.Value -= durationUs;
                else
                {
                    // Advance stage
                    stage.remainingUs.Value = FightTimeUs + BuyTimeUs;
                    stage.number.Value++;
                    SetActiveCures(stage);
                }
            }
        }

        public override void ModifyPlayer(SessionBase session, Container container, int playerId, Container player, Container commands, uint durationUs)
        {
            base.ModifyPlayer(session, container, playerId, player, commands, durationUs);

            var stage = container.Require<ShowdownSessionComponent>();
            if (stage.number.WithoutValue) return;

            bool isFightTime = stage.remainingUs < FightTimeUs;
            player.Require<FrozenProperty>().Value = !isFightTime;
            if (isFightTime)
            {
                var showdownPlayer = player.Require<ShowdownPlayerComponent>();

                PlayerSecuring(player, commands, showdownPlayer, stage, durationUs);
            }
            else
            {
                ByteProperty wantedBuyItemId = player.Require<MoneyComponent>().wantedBuyItemId;
                if (wantedBuyItemId.WithValue)
                {
                    UShortProperty money = player.Require<MoneyComponent>().count;
                    Debug.Log($"Trying to buy requested item: {wantedBuyItemId.Value}");
                    ushort cost = GetCost(wantedBuyItemId);
                    if (cost < money)
                    {
                        var inventory = player.Require<InventoryComponent>();
                        PlayerItemManagerModiferBehavior.AddItem(inventory, wantedBuyItemId);
                        money.Value -= cost;
                    }
                }
            }
        }
        
        public bool IsLookingAt<TBehavior>(Container player, out TBehavior behavior)
        {
            Ray ray = SessionBase.GetRayForPlayer(player);
            int count = Physics.RaycastNonAlloc(ray, m_CachedHits, 2.0f, m_ModelMask);
            if (count > 0 && m_CachedHits[0].collider.TryGetComponent(out behavior))
                return true;
            behavior = default;
            return false;
        }

        private void PlayerSecuring(Container player, Container commands, ShowdownPlayerComponent showdownPlayer, ShowdownSessionComponent stage, uint durationUs)
        {
            var isInteracting = false;
            CurePackageComponent cure = default;
            if (commands.Require<InputFlagProperty>().GetInput(PlayerInput.Interact) && IsLookingAt(player, out CurePackageBehavior curePackage))
            {
                cure = stage.curePackages[curePackage.Container.Require<IdProperty>()];
                if (cure.isActive.WithValue && cure.isActive) isInteracting = true;
            }
            if (isInteracting)
            {
                checked
                {
                    showdownPlayer.elapsedSecuringUs.Value += durationUs;
                    if (showdownPlayer.elapsedSecuringUs > SecureTimeUs)
                    {
                        Secure(showdownPlayer, stage, cure);
                    }
                }
            }
            else
            {
                showdownPlayer.elapsedSecuringUs.Value = 0u;
            }
        }

        private static void Secure(ShowdownPlayerComponent showdownPlayer, ShowdownSessionComponent stage, CurePackageComponent cure)
        {
            showdownPlayer.stagesCuredFlags.Value |= (byte) (1 << stage.number);
            cure.isActive.Value = false;
        }

        protected override void HandleRespawn(SessionBase session, Container container, int playerId, Container player, HealthProperty health, uint durationUs)
        {
            if (InWarmup(container)) base.HandleRespawn(session, container, playerId, player, health, durationUs); // Random respawn
        }

        private static void FirstStageSpawn(Container session, int playerId, Container player, QueuedTeamSpawns spawns)
        {
            player.Require<TeamProperty>().Value = (byte) (playerId % PlayersPerTeam);

            var move = player.Require<MoveComponent>();
            move.Zero();
            // TODO:refactor use rotation
            (Vector3 position, Container _) = spawns[player.Require<TeamProperty>()].Dequeue();
            move.position.Value = position;
            player.ZeroIfWith<CameraComponent>();
            player.Require<MoneyComponent>().count.Value = ushort.MaxValue;
            if (player.With(out HealthProperty health)) health.Value = 100;
            player.ZeroIfWith<HitMarkerComponent>();
            player.ZeroIfWith<DamageNotifierComponent>();
            if (player.With(out InventoryComponent inventory))
            {
                inventory.Zero();
                PlayerItemManagerModiferBehavior.AddItems(inventory, ItemId.Shovel, ItemId.Pistol);
            }
            player.Require<ShowdownPlayerComponent>().Zero();
        }
 
        private static void StartFirstStage(SessionBase session, Container sessionContainer, ShowdownSessionComponent stage)
        {
            ModelsProperty models = MapManager.Singleton.Map.models;
            QueuedTeamSpawns spawns = models.Where(modelTuple => modelTuple.Item2.With<TeamProperty>())
                                            .GroupBy(spawnTuple => spawnTuple.Item2.Require<TeamProperty>().Value)
                                            .Select(teamGroup => new Queue<(Position3Int, Container)>(teamGroup))
                                            .ToArray();
            stage.number.Value = 0;
            stage.remainingUs.Value = BuyTimeUs + FightTimeUs;
            SetActiveCures(stage);
            ForEachActivePlayer(session, sessionContainer, (playerId, player) => FirstStageSpawn(sessionContainer, playerId, player, spawns));
            Debug.Log("Started first stage");
        }

        private static void SetActiveCures(ShowdownSessionComponent stage)
        {
            if (stage.number == 0)
            {
                for (var index = 0; index < stage.curePackages.Length; index++)
                {
                    CurePackageComponent package = stage.curePackages[index];
                    package.isActive.Value = index % 2 == 1;
                }
                return;
            }

            var choseFrom = new List<int>();
            for (var index = 0; index < stage.curePackages.Length; index++)
            {
                CurePackageComponent package = stage.curePackages[index];
                if (package.isActive) package.isActive.Value = false;
                else choseFrom.Add(index);
            }
            choseFrom.RemoveAt(Random.Range(0, choseFrom.Count));
            foreach (int index in choseFrom)
            {
                stage.curePackages[index].isActive.Value = true;
            }
        }

        // TODO:performance LINQ creates too much garbage?
        private static int GetPlayerCount(Container session)
            => session.Require<PlayerContainerArrayElement>().Count(player => player.Require<HealthProperty>().WithValue);

        private static bool InWarmup(Container session) => session.Require<ShowdownSessionComponent>().number.WithoutValue;

        protected override float CalculateWeaponDamage(SessionBase session, Container hitPlayer, Container inflictingPlayer,
                                                       PlayerHitbox hitbox, WeaponModifierBase weapon, in RaycastHit hit)
        {
            if (!InWarmup(session.GetLatestSession()) && hitPlayer.Require<TeamProperty>() == inflictingPlayer.Require<TeamProperty>()) return 0.0f;

            float baseDamage = base.CalculateWeaponDamage(session, hitPlayer, inflictingPlayer, hitbox, weapon, hit);
            return CalculateDamageWithMovement(session, inflictingPlayer, weapon, baseDamage);
        }

        public static float CalculateDamageWithMovement(SessionBase session, Container inflictingPlayer, WeaponModifierBase weapon, float baseDamage)
        {
            if (weapon is MeleeModifier) return baseDamage;
            // Nerf damage while on the run
            Vector3 velocity = inflictingPlayer.Require<MoveComponent>().velocity;
            var modifierPrefab = (PlayerModifierDispatcherBehavior) session.PlayerManager.GetModifierPrefab(inflictingPlayer.Require<IdProperty>());
            float ratio = 1.0f - Mathf.Clamp01(velocity.LateralMagnitude() / modifierPrefab.Movement.MaxSpeed);
            const float minimumRatio = 0.3f;
            ratio = minimumRatio + ratio * (1.0f - minimumRatio);
            return baseDamage * ratio;
        }

        public override bool AllowTeamSwap(Container container, Container player) => InWarmup(container);

        public override void Render(SessionBase session, Container sessionContainer)
        {
            if (MapManager.Singleton.Models.Count == 0) return;
            ArrayElement<CurePackageComponent> cures = sessionContainer.Require<ShowdownSessionComponent>().curePackages;
            // TODO:performance
            m_CurePackages = MapManager.Singleton.Models.Values
                                       .Where(model => model.Container.Require<ModelIdProperty>() == ModelsProperty.Cure)
                                       .Cast<CurePackageBehavior>()
                                       .ToArray();
            for (var index = 0; index < cures.Length; index++)
                m_CurePackages[index].Render(cures[index]);
        }

        // public override void SetupNewPlayer(SessionBase session, Container player)
        // {
        //     Container container = session.GetLatestSession();
        //     if (container.Require<ShowdownSessionComponent>().number == WarmupNumber)
        //     {
        //         player.Require<TeamProperty>().Value = 
        //     }
        // }
    }
}