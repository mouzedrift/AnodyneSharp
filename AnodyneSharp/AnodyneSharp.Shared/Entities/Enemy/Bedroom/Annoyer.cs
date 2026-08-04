using AnodyneSharp.Drawing;
using AnodyneSharp.Entities.Base.Rendering;
using AnodyneSharp.Entities.Enemy.Circus;
using AnodyneSharp.FSM;
using AnodyneSharp.Registry;
using AnodyneSharp.Sounds;
using AnodyneSharp.Utilities;
using Microsoft.Xna.Framework;
using RSG;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnodyneSharp.Entities.Enemy;

//MapCollision conditionally enabled with Solid property
[NamedEntity, Enemy, Collision(typeof(Player), typeof(Broom), MapCollision = true, KeepOnScreen = true)]
public class Annoyer : Entity
{
    public static AnimatedSpriteRenderer GetSprite(int frame)
    {
        int[] frames;
        int framerate = 4;
        if(GlobalState.IsCell)
        {
            frames = new int[] { 6, 7 };
        }
        else if(GlobalState.BoiEaster)
        {
            frames = new int[] { 8, 9 };
        }
        else
        {
            int i = frame == T_SUPER ? 12 : 0;
            frames = new int[] { i, i + 1, i + 2, i + 3, i + 4, i + 5 };
            framerate = 8;
        }
        return new("annoyer", 16, 16, new Anim("flap", frames, framerate));
    }

    private int _health = 1;

    private EntityPreset _preset;

    private Player _target;

    public const string DamageDealer = "Annoyer";
    public const string FireballDamageDealer = "Annoyer fireball";

    private const int T_SUPER = 2;

    private const float rotation_radius = 20f;
    private AnnoyerState _startState = AnnoyerState.Wait;

    private Explosion explosion;
    private EntityPool<Fireball> fireballs;

    private Vector2 ApproachTarget
    {
        get
        {
            return _target.Center + Vector2.UnitX * rotation_radius;
        }
    }

    private enum AnnoyerState
    {
        Wait,
        Approach,
        Circle,
        Swoop,
        Hit
    }

    private AnnoyerState _state;
    private float _stateTimer;
    private float _circleAngle;
    private Vector2 _swoopTarget;
    private float _fireTimer;

    public Annoyer(EntityPreset preset, Player player) 
        : base(preset.Position, GetSprite(preset.Frame), DrawOrder.ENTITIES)
    {
        MapInteraction = false;
        _preset = preset;
        _target = player;
        Solid = false;
        height = 7;
        width = 8;
        offset.X = 3;
        offset.Y = 2;

        explosion = new Explosion(Position)
        {
            exists = false
        };

        if (preset.Frame == T_SUPER)
            _health = 2;

        fireballs = new EntityPool<Fireball>(preset.Frame == T_SUPER ? 4 : 0, () => new Fireball());

        ChangeState(AnnoyerState.Wait);
    }

    private void ResetFireTimer()
    {
        _fireTimer = 2.3f;
    }

    private void ChangeState(AnnoyerState newState)
    {
        // Exit behavior
        if (_state == AnnoyerState.Approach)
        {
            velocity = Vector2.Zero;
        }

        _state = newState;

        switch (_state)
        {
            case AnnoyerState.Wait:
                velocity = Vector2.Zero;
                _stateTimer = 0.25f;
                ResetFireTimer();
                break;

            case AnnoyerState.Circle:
                _circleAngle = 0;
                break;

            case AnnoyerState.Swoop:
                _swoopTarget = Position + 3 * (_target.Position - Position);
                break;

            case AnnoyerState.Hit:
                SoundManager.PlaySoundEffect("player_hit_1");
                Flicker(0.2f);

                if (--_health <= 0)
                {
                    Solid = true;
                    _stateTimer = 0.25f;
                }
                else
                {
                    _stateTimer = 0.4f;
                }
                break;
        }
    }

    public override void Update()
    {
        if (_state != AnnoyerState.Hit)
        {
            _fireTimer -= GameTimes.DeltaTime;

            if (_fireTimer <= 0f)
            {
                fireballs.Spawn(f => f.Spawn(this, _target));
                _fireTimer += 2.3f;
            }
        }

        switch (_state)
        {
            case AnnoyerState.Wait:
                _stateTimer -= GameTimes.DeltaTime;

                if (_stateTimer <= 0)
                {
                    if ((Position - _target.Position).Length() < 64)
                    {
                        ChangeState(AnnoyerState.Approach);
                    }
                    else
                    {
                        _stateTimer = 0.25f;
                    }
                }
                break;

            case AnnoyerState.Approach:
                MathUtilities.MoveTo(ref Position.X, ApproachTarget.X, 36);
                MathUtilities.MoveTo(ref Position.Y, ApproachTarget.Y, 36);

                if ((Position - ApproachTarget).Length() < 2)
                {
                    ChangeState(AnnoyerState.Circle);
                }

                break;

            case AnnoyerState.Circle:
                _circleAngle += 8.4f * GameTimes.DeltaTime;

                Position = _target.VisualCenter +
                    new Vector2(
                        (float)Math.Cos(_circleAngle),
                        (float)Math.Sin(_circleAngle)
                    ) * rotation_radius;

                break;

            case AnnoyerState.Swoop:
                if (MathUtilities.MoveTo(ref Position.X, _swoopTarget.X, 2.5f * 60) &
                    MathUtilities.MoveTo(ref Position.Y, _swoopTarget.Y, 2.5f * 60))
                {
                    ChangeState(AnnoyerState.Approach);
                }
                break;

            case AnnoyerState.Hit:
                _stateTimer -= GameTimes.DeltaTime;

                if (_stateTimer <= 0)
                {
                    if (_health <= 0)
                    {
                        exists = _preset.Alive = false;
                        explosion.exists = true;
                        explosion.Position = Position;
                        SoundManager.PlaySoundEffect("hit_wall");
                    }
                    else
                    {
                        Solid = false;
                        ChangeState(_startState);
                        _startState = AnnoyerState.Approach;
                    }
                }
                break;
        }

        base.Update();
    }

    public override IEnumerable<Entity> SubEntities()
    {
        return fireballs.Entities.Concat(Enumerable.Repeat(explosion,1));
    }

    public override void Collided(Entity other)
    {
        if (_state == AnnoyerState.Hit)
        {
            return;
        }

        if (other is Player p)
        {
            p.ReceiveDamage(1, DamageDealer);
        }
        else if (other is Broom b)
        {
            velocity = FacingDirection(b.facing) * 150;

            if (velocity.Y < 0)
            {
                velocity.X = GlobalState.RNG.Next(-30, 31);
            }

            ChangeState(AnnoyerState.Hit);
        }
    }

    public class Explosion : HealthDropper
    {
        public static AnimatedSpriteRenderer GetSprite(int o) => new AnimatedSpriteRenderer("enemy_explode_2", 24, 24, new Anim("explode", new int[] { o, o + 1, o + 2, o + 3, o + 4 }, GlobalState.IsCell ? 10 : 12, false));
        public Explosion(Vector2 pos) : base(null, pos, GetSprite(GlobalState.IsCell ? 5 : 0), DrawOrder.ENTITIES)
        {
        }

        public override void Update()
        {
            base.Update();
            if(AnimFinished)
            {
                Die();
            }
        }
    }

    [Collision(typeof(Player),typeof(Broom),MapCollision = false)]
    public class Fireball : Entity
    {
        private const float speed = 30f;
        private enum FireballState
        {
            Shoot,
            Poof
        }

        private FireballState _state;

        public Fireball() : base(Vector2.Zero, Lion.Fireball.GetSprite(8), DrawOrder.FG_SPRITES)
        {
            width = height = 8;
            offset = new Vector2(4, 4);
        }

        private void ChangeState(FireballState newState)
        {
            _state = newState;

            switch (_state)
            {
                case FireballState.Shoot:
                    Play("shoot");
                    break;

                case FireballState.Poof:
                    Play("poof");
                    break;
            }
        }

        public void Spawn(Entity parent, Entity target)
        {
            Position = parent.Position;
            MoveTowards(target.Position, speed);
            opacity = 1.0f;
            ChangeState(FireballState.Shoot);
        }

        public override void Collided(Entity other)
        {
            if (_state != FireballState.Shoot)
            {
                return;
            }

            if (other is Player p)
            {
                p.ReceiveDamage(1, FireballDamageDealer);
                ChangeState(FireballState.Poof);
            }
            else if (other is Broom)
            {
                ChangeState(FireballState.Poof);
            }
        }

        public override void Update()
        {
            switch (_state)
            {
                case FireballState.Shoot:
                    opacity -= 0.06f * GameTimes.DeltaTime;

                    if (opacity <= 0.6f)
                    {
                        ChangeState(FireballState.Poof);
                    }
                    break;

                case FireballState.Poof:
                    if (AnimFinished)
                    {
                        exists = false;
                    }
                    break;
            }

            base.Update();
        }
    }
}
