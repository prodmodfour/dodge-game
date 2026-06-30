using System.Collections;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Actions
{
    /// <summary>
    /// Lightweight placeholder presentation for melee resolution. It gives the attacker
    /// an immediate horizontal facing update and, in play mode, a tiny weaponless lunge
    /// so prototype testers can see that the melee action resolved.
    /// </summary>
    public static class MeleeAttackPresentation
    {
        private const float MinimumDirectionSqrMagnitude = 0.0001f;
        private const float LungeDistance = 0.18f;
        private const float LungeDurationSeconds = 0.16f;
        private const float FallbackDeltaTime = 1f / 60f;

        /// <summary>
        /// Faces the actor toward the target and starts a short non-blocking lunge while in play mode.
        /// Returns true when a usable facing direction was found.
        /// </summary>
        public static bool Play(TacticalUnit actor, TacticalUnit target)
        {
            if (actor == null || target == null)
            {
                return false;
            }

            if (!TryGetFacingDirection(actor, target, out var facingDirection))
            {
                return false;
            }

            FaceActor(actor.transform, facingDirection);
            TryStartLunge(actor, facingDirection);
            return true;
        }

        private static bool TryGetFacingDirection(TacticalUnit actor, TacticalUnit target, out Vector3 facingDirection)
        {
            var worldDelta = target.transform.position - actor.transform.position;
            worldDelta.y = 0f;
            if (worldDelta.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                facingDirection = worldDelta.normalized;
                return true;
            }

            var actorGrid = actor.CurrentGridPosition;
            var targetGrid = target.CurrentGridPosition;
            var gridDelta = new Vector3(targetGrid.X - actorGrid.X, 0f, targetGrid.Z - actorGrid.Z);
            if (gridDelta.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                facingDirection = gridDelta.normalized;
                return true;
            }

            var currentForward = actor.transform.forward;
            currentForward.y = 0f;
            if (currentForward.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                facingDirection = currentForward.normalized;
                return true;
            }

            facingDirection = Vector3.forward;
            return false;
        }

        private static void FaceActor(Transform actorTransform, Vector3 facingDirection)
        {
            actorTransform.rotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        }

        private static void TryStartLunge(TacticalUnit actor, Vector3 facingDirection)
        {
            if (!Application.isPlaying || !actor.isActiveAndEnabled)
            {
                return;
            }

            actor.StartCoroutine(PlayLungeCoroutine(actor.transform, facingDirection));
        }

        private static IEnumerator PlayLungeCoroutine(Transform actorTransform, Vector3 facingDirection)
        {
            if (actorTransform == null)
            {
                yield break;
            }

            var startPosition = actorTransform.position;
            var lungePosition = startPosition + (facingDirection * LungeDistance);
            var halfDuration = LungeDurationSeconds * 0.5f;

            yield return MoveBetween(actorTransform, startPosition, lungePosition, halfDuration);
            yield return MoveBetween(actorTransform, lungePosition, startPosition, halfDuration);

            if (actorTransform != null)
            {
                actorTransform.position = startPosition;
            }
        }

        private static IEnumerator MoveBetween(Transform actorTransform, Vector3 from, Vector3 to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (actorTransform == null)
                {
                    yield break;
                }

                elapsed += GetFrameDeltaTime();
                var t = Mathf.Clamp01(elapsed / duration);
                actorTransform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            if (actorTransform != null)
            {
                actorTransform.position = to;
            }
        }

        private static float GetFrameDeltaTime()
        {
            var deltaTime = Time.deltaTime;
            return IsPositiveFinite(deltaTime) ? deltaTime : FallbackDeltaTime;
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
