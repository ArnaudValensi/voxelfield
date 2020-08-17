using Swihoni.Components;
using Swihoni.Sessions.Modes;
using Swihoni.Sessions.Player;
using Swihoni.Sessions.Player.Components;
using Swihoni.Util;
using UnityEngine;

namespace Swihoni.Sessions.Entities
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableModifierBehavior : EntityModifierBehavior
    {
        private enum CollisionType
        {
            None,
            World,
            Player
        }

        [SerializeField] private uint m_PopTimeUs = default, m_PopDurationUs = default;
        [SerializeField] protected float m_Radius = default;
        [SerializeField] private float m_Damage = default, m_Interval = default;
        [SerializeField] private LayerMask m_Mask = default;
        [SerializeField] private float m_CollisionVelocityMultiplier = 0.5f;
        [SerializeField] protected bool m_IsSticky, m_ExplodeOnContact;
        [SerializeField] private AnimationCurve m_DamageCurve = default;
        [SerializeField] private float m_PlayerForce = 2.0f;
        
        private RigidbodyConstraints m_InitialConstraints;
        private readonly Collider[] m_OverlappingColliders = new Collider[8];
        private uint m_LastElapsedUs;
        private bool m_IsFrozen;
        private (CollisionType, Collision) m_LastCollision;

        public string Name { get; set; }
        public Rigidbody Rigidbody { get; private set; }
        public bool PopQueued { get; set; }
        public bool CanQueuePop => m_PopTimeUs == uint.MaxValue;
        public float Radius => m_Radius;

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            m_InitialConstraints = Rigidbody.constraints;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (m_IsFrozen) return;
            bool isInMask = (m_Mask & (1 << collision.gameObject.layer)) != 0;
            m_LastCollision = (isInMask ? CollisionType.Player : CollisionType.World, collision);
        }

        private void ResetRigidbody(bool canMove)
        {
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            Rigidbody.constraints = canMove ? m_InitialConstraints : RigidbodyConstraints.FreezeAll;
        }

        public override void SetActive(bool isActive, int index)
        {
            base.SetActive(isActive, index);
            ResetRigidbody(isActive);
            PopQueued = false;
            m_LastElapsedUs = 0u;
            m_LastCollision = (CollisionType.None, null);
            m_IsFrozen = false;
        }

        private static Vector3 GetSurfaceNormal(Collision collision)
        {
            Vector3 point = collision.contacts[0].point,
                    direction = collision.contacts[0].normal;
            point += direction;
            return collision.collider.Raycast(new Ray(point, -direction), out RaycastHit hit, 2.0f)
                ? hit.normal
                : Vector3.up;
        }

        public override void Modify(in SessionContext context)
        {
            Container entity = context.entity;

            var throwable = entity.Require<ThrowableComponent>();
            throwable.thrownElapsedUs.Add(context.durationUs);

            bool poppedFromTime = throwable.thrownElapsedUs >= m_PopTimeUs && m_LastElapsedUs < throwable.popTimeUs;
            if (poppedFromTime || PopQueued)
            {
                throwable.popTimeUs.Value = throwable.thrownElapsedUs;
                PopQueued = false;
            }

            bool hasPopped = throwable.thrownElapsedUs >= throwable.popTimeUs;
            if (hasPopped) m_IsFrozen = true;
            Transform t = transform;
            if (hasPopped)
            {
                t.rotation = Quaternion.identity;

                bool justPopped = m_LastElapsedUs < throwable.popTimeUs;

                if (m_Interval > 0u || justPopped)
                    HurtNearby(context, entity, throwable, justPopped);
            }
            else
            {
                throwable.contactElapsedUs.Add(context.durationUs);
                (CollisionType collisionType, Collision collision) = m_LastCollision;
                if (collisionType != CollisionType.None)
                {
                    var resetContact = true;
                    if (m_ExplodeOnContact && (collisionType == CollisionType.World
                                            || collision.collider.TryGetComponent(out PlayerTrigger trigger) && trigger.PlayerId != throwable.throwerId))
                    {
                        throwable.popTimeUs.Value = throwable.thrownElapsedUs;
                        resetContact = false;
                        HurtNearby(context, entity, throwable, true);
                    }
                    if (collisionType == CollisionType.World && !m_ExplodeOnContact)
                    {
                        if (m_IsSticky)
                        {
                            ResetRigidbody(false);
                            Vector3 surfaceNormal = GetSurfaceNormal(collision), position = collision.contacts[0].point;
                            Quaternion rotation = Quaternion.FromToRotation(Rigidbody.transform.up, surfaceNormal) * Rigidbody.rotation;
                            Rigidbody.transform.SetPositionAndRotation(position, rotation);
                            m_IsFrozen = true;
                        }
                        else
                        {
                            Rigidbody.velocity *= m_CollisionVelocityMultiplier;
                        }
                    }
                    else if (throwable.thrownElapsedUs > 100_000u) Rigidbody.velocity = Vector3.zero; // Stop on hitting player. Delay to prevent hitting self
                    if (resetContact) throwable.contactElapsedUs.Value = 0u;
                }
            }
            m_LastElapsedUs = throwable.thrownElapsedUs;
            m_LastCollision = (CollisionType.None, null);
            Rigidbody.constraints = m_IsFrozen ? RigidbodyConstraints.FreezeAll : m_InitialConstraints;

            base.Modify(context); // Set position and rotation

            if (throwable.popTimeUs != uint.MaxValue && throwable.thrownElapsedUs - throwable.popTimeUs > m_PopDurationUs)
                entity.Clear();
        }

        private void HurtNearby(in SessionContext context, Container entity, ThrowableComponent throwable, bool justPopped)
        {
            if (justPopped) JustPopped(context, entity);
            if (m_Damage < Mathf.Epsilon) return;
            int count = Physics.OverlapSphereNonAlloc(transform.position, m_Radius, m_OverlappingColliders, m_Mask);
            for (var i = 0; i < count; i++)
            {
                Collider hitCollider = m_OverlappingColliders[i];
                if (!hitCollider.TryGetComponent(out PlayerTrigger trigger)) continue;
                int hitPlayerId = trigger.PlayerId;
                Container hitPlayer = context.GetModifyingPlayer(hitPlayerId);
                if (hitPlayer.WithPropertyWithValue(out HealthProperty health) && health.IsAlive)
                {
                    byte damage = CalculateDamage(new SessionContext(player: hitPlayer, durationUs: context.durationUs));
                    int inflictingPlayerId = throwable.throwerId;
                    Container inflictingPlayer = context.GetModifyingPlayer(inflictingPlayerId);
                    if (inflictingPlayer.Require<TeamProperty>() == hitPlayer.Require<TeamProperty>()) damage /= 2;
                    var playerContext = new SessionContext(existing: context, playerId: inflictingPlayerId, player: inflictingPlayer);
                    var damageContext = new DamageContext(playerContext, hitPlayerId, hitPlayer, damage, Name);
                    context.ModifyingMode.InflictDamage(damageContext);
                }
                if (hitPlayer.With(out MoveComponent move))
                {
                    Vector3 direction = hitCollider.transform.position - transform.position;
                    float magnitude = 1.0f - Mathf.Clamp01(direction.magnitude / m_Radius);
                    direction.Normalize();
                    move.velocity.Value += direction * (magnitude * m_PlayerForce);
                }
            }
        }

        protected virtual void JustPopped(in SessionContext context, Container entity) => context.session.Injector.OnThrowablePopped(this, entity);
        
        private byte CalculateDamage(in SessionContext context)
        {
            float distance = Vector3.Distance(context.player.Require<MoveComponent>(), transform.position);
            float ratio = Mathf.Clamp01(distance / m_Radius);
            if (m_Interval > 0u) ratio *= context.durationUs * TimeConversions.MicrosecondToSecond;
            return (byte) Mathf.RoundToInt(m_DamageCurve.Evaluate(ratio) * m_Damage);
        }
    }
}