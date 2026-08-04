using AnodyneSharp.Drawing;
using AnodyneSharp.Entities.Base.Rendering;
using AnodyneSharp.Entities.Gadget;
using AnodyneSharp.Registry;
using AnodyneSharp.Sounds;
using AnodyneSharp.Utilities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnodyneSharp.Entities.Enemy;

[NamedEntity, Enemy, Collision(typeof(Player), typeof(Broom), MapCollision = true, KeepOnScreen = true)]
public class Slime : HealthDropper
{
    private enum SlimeType
    {
        Normal,
        //Key, unused
        //Rise, unused
        Bullet = 3
    }

    public const string DamageDealer = "Slime";
    public const string BulletDamageDealer = "Slime bullet";
    private SlimeType _type;

    private int _health = 2;

    private enum State
    {
        Move,
        Hurt,
        Dying
    }

    private State _state;

    private float _moveTimer;
    private float _shootTimer;
    private bool _moveFrameSoundSync;
    private DateTime _moveExitTime;

    private float _speed = 20f;

    private EntityPool<Goo> goos;
    private EntityPool<Bullet> bullets;

    private Player target;

    public static AnimatedSpriteRenderer GetSprite()
    {
        int o = GlobalState.IsCell ? 4 : (GlobalState.BoiEaster ? 2 : 0);
        return new("slime", 16, 16,
            new Anim("Move", new int[] { o, o + 1 }, 3),
            new Anim("Hurt", new int[] { o, 6, o, 6 }, 15, false),
            new Anim("Dying", new int[] { o, 6, o, 6 }, 12, false)
            );
    }

    public Slime(EntityPreset preset, Player player)
        : base(preset, preset.Position, GetSprite(), DrawOrder.ENTITIES)
    {
        _type = preset.Frame == 3 ? SlimeType.Bullet : SlimeType.Normal;

        goos = new EntityPool<Goo>(8, () => new Goo());
        target = player;

        if (_type == SlimeType.Bullet)
        {
            bullets = new EntityPool<Bullet>(4, () => new Bullet());
            _speed *= 2;
        }

        ChangeState(State.Move);
    }

    private void ChangeState(State newState)
    {
        switch (_state)
        {
            case State.Move:
                _moveExitTime = DateTime.Now;
                break;
        }

        _state = newState;

        switch (_state)
        {
            case State.Move:
                Play("Move");

                _moveTimer += (float)(DateTime.Now - _moveExitTime).TotalSeconds;

                if (_type == SlimeType.Bullet)
                {
                    _shootTimer = 1.8f;
                }
                break;

            case State.Hurt:
                Play("Hurt");
                break;

            case State.Dying:
                Play("Dying");
                break;
        }
    }

    private void SyncSplash()
    {
        if (Frame == 1 && !_moveFrameSoundSync)
        {
            SoundManager.PlaySoundEffect("slime_walk");
            _moveFrameSoundSync = true;
        }
        else if (Frame == 0)
        {
            _moveFrameSoundSync = false;
        }
    }

    private void ChangeDir()
    {
        if (Frame == 1)
        {
            //Make it more likely for slimes to stand still periodically
            velocity = Vector2.Zero;
        }
        else
        {
            velocity = new Vector2((float)GlobalState.RNG.NextDouble(), (float)GlobalState.RNG.NextDouble()) - Vector2.One / 2f;
            velocity *= _speed;
        }
    }

    private void GetHit(Broom b)
    {
        SoundManager.PlaySoundEffect("hit_slime");

        goos.Spawn(g => g.Spawn(this), 2);

        _health -= 1;
        velocity = FacingDirection(b.facing) * 100;

        ChangeState(State.Hurt);
    }
    private void UpdateMove()
    {
        SyncSplash();

        _moveTimer -= GameTimes.DeltaTime;
        if (_moveTimer <= 0f)
        {
            _moveTimer += 0.5f;
            ChangeDir();
        }

        if (_type == SlimeType.Bullet)
        {
            _shootTimer -= GameTimes.DeltaTime;
            if (_shootTimer <= 0f)
            {
                _shootTimer += 1.8f;
                bullets.Spawn(b => b.Spawn(this, target));
            }
        }
    }

    private void UpdateHurt()
    {
        if (_health <= 0)
        {
            ChangeState(State.Dying);
            return;
        }

        if (AnimFinished)
        {
            ChangeState(State.Move);
        }
    }

    private void UpdateDying()
    {
        if (AnimFinished)
        {
            GlobalState.SpawnEntity(new Explosion(this));
            Die();
        }
    }

    public override void Update()
    {
        switch (_state)
        {
            case State.Move:
                UpdateMove();
                break;

            case State.Hurt:
                UpdateHurt();
                break;

            case State.Dying:
                UpdateDying();
                break;
        }

        base.Update();
    }

    public override void Collided(Entity other)
    {
        if (other is Player p)
        {
            p.ReceiveDamage(1, DamageDealer);
        }
        else if (other is Broom b)
        {
            if (_state == State.Move)
            {
                GetHit(b);
            }
        }
    }

    public override IEnumerable<Entity> SubEntities()
    {
        if (_type == SlimeType.Normal)
        {
            return goos.Entities.Concat(base.SubEntities());
        }
        else
        {
            return goos.Entities.Concat(bullets.Entities).Concat(base.SubEntities());
        }

    }

    [Collision(MapCollision = true)]
    public class Goo : Entity
    {
        private enum GooState
        {
            Move,
            Splash
        }

        private GooState _state;
        private Parabola_Thing _parabola;

        public static AnimatedSpriteRenderer GetSprite(int framerate)
        {
            int o = GlobalState.IsCell || GlobalState.BoiEaster ? 4 : 0;
            return new("slime_goo", 6, 6,
                new Anim("move", new int[] { 0 + o, 1 + o, 2 + o, 3 + o, 1 + o, 3 + o, 1 + o, 2 + o, 1 + o, 0 + o }, framerate),
                new Anim("splash", new int[] { o }, 1));
        }

        public Goo() : base(Vector2.Zero, GetSprite(GlobalState.RNG.Next(5, 10)), DrawOrder.PARTICLES)
        {
            shadow = new Shadow(this, Vector2.Zero, ShadowType.Tiny);

            ChangeState(GooState.Move);
        }

        private void ChangeState(GooState newState)
        {
            _state = newState;

            switch (_state)
            {
                case GooState.Move:
                    _parabola = new Parabola_Thing(this, 16, 0.8f + 0.3f * (float)GlobalState.RNG.NextDouble());

                    velocity.X = MathUtilities.OneRandomOf(-1, 1) * (10 + 5 * (float)GlobalState.RNG.NextDouble());
                    velocity.Y = MathUtilities.OneRandomOf(-1, 1) * (10 + 5 * (float)GlobalState.RNG.NextDouble());

                    Play("move");
                    shadow.exists = true;
                    opacity = 1.0f;
                    break;

                case GooState.Splash:
                    SoundManager.PlaySoundEffect("slime_splash");

                    shadow.exists = false;
                    Play("splash");
                    velocity = Vector2.Zero;
                    break;
            }
        }

        public void Spawn(Slime parent)
        {
            Position = parent.Position;
            ChangeState(GooState.Move);
        }

        public override void Update()
        {
            base.Update();

            switch (_state)
            {
                case GooState.Move:
                    if (_parabola.Tick())
                    {
                        ChangeState(GooState.Splash);
                    }
                    break;

                case GooState.Splash:
                    opacity -= 0.05f;

                    if (opacity <= 0f)
                    {
                        exists = false;
                    }
                    break;
            }
        }
    }

    [Collision(typeof(Player), MapCollision = true)]
    public class Bullet : Entity
    {

        public Bullet()
            : base(Vector2.Zero, new AnimatedSpriteRenderer("slime_bullet", 8, 8, new Anim("move", new int[] { 0, 1 }, GlobalState.RNG.Next(5, 10))), DrawOrder.PARTICLES)
        {
        }

        public void Spawn(Slime parent, Player target)
        {
            SoundManager.PlaySoundEffect("slime_shoot");

            Position = parent.Position;
            velocity = Vector2.Normalize(target.Position - parent.Position) * 40;

            Play("move");
            opacity = 1.0f;
        }

        public override void Update()
        {
            base.Update();

            opacity -= 0.39f * GameTimes.DeltaTime;

            if (touching != Touching.NONE || opacity < 0.3f)
            {
                exists = false;
            }
        }

        public override void Collided(Entity other)
        {
            if (other is Player p && p.state != PlayerState.AIR)
            {
                p.ReceiveDamage(1, BulletDamageDealer);
                exists = false;
            }
        }
    }
}