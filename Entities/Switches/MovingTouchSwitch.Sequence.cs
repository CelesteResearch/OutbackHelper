using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Components.Animation;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Randomness;
using Celeste.Core.Scheduling;
using Celeste.Simulation;


namespace Celeste.Mods.OutbackHelper.Entities.Switches
{
    public partial class MovingTouchSwitch
    {
        private IEnumerator TriggeredSwitch()
        {
            if (isMoving) yield break;
            isMoving = true;
            Level.Shake(.1f);
            if (nodeIndex < touchSwitchNodes.Length)
            {
                process.AudioSession.Play("event:/game/general/crystalheart_bounce", Position, process.SaveContext);
                for (int i = 0; i < 16; i++)
                {
                    float angle = SourceRandomMath.NextFloat(process.Random, (float)Math.PI * 2f);
                    Level.Particles.Emit(process.ParticleTypes.TouchSwitch.P_FireWhite,
                        Position + Vector2Geometry.AngleToVector(angle, 6f), angle);
                }

                Vector2 targetPosition = touchSwitchNodes[nodeIndex];
                Add(new SimulationCoroutine(process, DrawPathParticles(Center, targetPosition + new Vector2(7f, 7f))));
                SourceTweenState.Position(process.TweenPool, this, targetPosition, MovingDelay,
                    ((ISourceEasingEnvironment)process).EasingArithmetic.SineOut, process.ScalarArithmetic);
                while (Position != targetPosition)
                {
                    icon.Color = MovingColor;
                    yield return null;
                }

                for (int i = 0; i < 32; i++)
                {
                    float angle = SourceRandomMath.NextFloat(process.Random, (float)Math.PI * 2f);
                    Level.Particles.Emit(process.ParticleTypes.TouchSwitch.P_FireWhite,
                        Position + Vector2Geometry.AngleToVector(angle, 6f), angle);
                }

                process.AudioSession.Play("event:/game/04_cliffside/greenbooster_dash", Position, process.SaveContext);
                nodeIndex++;
            }
            else if (nodeIndex >= touchSwitchNodes.Length) TurnOn();

            isMoving = false;
        }

        private IEnumerator DrawPathParticles(Vector2 start, Vector2 end)
        {
            Vector2 currentPosition = start;
            while (currentPosition != end)
            {
                currentPosition = new Vector2(
                    ScalarMath.LerpSnap(currentPosition.X, end.X, .5f, 5f, process.ScalarArithmetic),
                    ScalarMath.LerpSnap(currentPosition.Y, end.Y, .5f, 5f, process.ScalarArithmetic));
                Level.Particles.Emit(nodeIndex + 1 == touchSwitchNodes.Length
                    ? FireParticle
                    : process.ParticleTypes.TouchSwitch.P_FireWhite, currentPosition);
                yield return null;
            }
        }
    }
}
