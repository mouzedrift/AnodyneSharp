using AnodyneSharp.Drawing;
using AnodyneSharp.Entities.Base.Rendering;
using AnodyneSharp.Entities.Gadget;
using AnodyneSharp.FSM;
using AnodyneSharp.Registry;
using AnodyneSharp.Sounds;
using AnodyneSharp.Utilities;
using Microsoft.Xna.Framework;
using RSG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnodyneSharp.Entities.Enemy.Apartment;

[NamedEntity("Gasguy"), Enemy, Collision(typeof(Player), typeof(Broom), KeepOnScreen = true, MapCollision = false)]

public class GasGuy : HealthDropper
{
    public static AnimatedSpriteRenderer GetSprite() => new("gas_guy", 16, 24, new Anim("float", new int[] { 0, 1 }, 2), new Anim("release_gas", new int[] { 2 }, 20));

    public const string DamageDealer = "Gas Guy";
    private int _health = 3;
    private enum GasGuyState
    {
        InitialLatency,
        Normal,
        Shoot
    }

    private GasGuyState _state;
    private float _stateTimer;

    private float soundDelay;
    private float soundDelayMax;

    private Player _player;

    private bool _inDelay;
    private bool _didFirstShot;

    private EntityPool<Gas> gasClouds;

    public GasGuy(EntityPreset preset, Player player)
        : base(preset, preset.Position, GetSprite(), DrawOrder.ENTITIES, 0.6f, true)
    {
        _player = player;

        drag = new Vector2(30);

        soundDelay = 0;
        soundDelayMax = (float)(1.5 + GlobalState.RNG.NextDouble());

        gasClouds = new EntityPool<Gas>(3, () => new Gas());

        ChangeState(GasGuyState.InitialLatency);
    }

    private void ChangeState(GasGuyState newState)
    {
        _state = newState;

        switch (_state)
        {
            case GasGuyState.InitialLatency:
                _stateTimer = 1.5f;
                _inDelay = true;
                break;

            case GasGuyState.Normal:
                _stateTimer = _didFirstShot ? 5f : 1f;
                Play("float");
                break;

            case GasGuyState.Shoot:
                _stateTimer = 1.2f;
                break;
        }
    }

    public override void Update()
    {
        base.Update();

        _stateTimer -= GameTimes.DeltaTime;

        switch (_state)
        {
            case GasGuyState.InitialLatency:
                if (_stateTimer <= 0f)
                {
                    _inDelay = false;
                    ChangeState(GasGuyState.Normal);
                }
                break;

            case GasGuyState.Normal:
                if (_stateTimer <= 0f)
                {
                    Play("release_gas");
                    _didFirstShot = true;
                    ChangeState(GasGuyState.Shoot);
                }
                break;

            case GasGuyState.Shoot:
                if (_stateTimer <= 0f)
                {
                    Play("float");
                    SoundManager.PlaySoundEffect("gasguy_shoot");
                    gasClouds.Spawn(s => s.Spawn(this, _player), 3, true);

                    ChangeState(GasGuyState.Normal);
                }
                break;
        }

        soundDelay += GameTimes.DeltaTime;

        if (soundDelay > soundDelayMax)
        {
            soundDelay = 0;
            SoundManager.PlaySoundEffect("gasguy_move");
        }

        if (!_inDelay)
        {
            MathUtilities.MoveTo(ref Position.X, _player.Position.X, 12f);
            MathUtilities.MoveTo(ref Position.Y, _player.Position.Y, 12f);
        }
    }

    public override void Collided(Entity other)
    {
        if (other is Player p)
        {
            p.ReceiveDamage(1, DamageDealer);
        }
        else if (other is Broom)
        {
            if (!_flickering)
            {
                _health--;
                Flicker(0.5f);

                MoveTowards(_player.Position, 100);
                velocity *= -1;

                SoundManager.PlaySoundEffect("broom_hit");

                if (_health == 0)
                {
                    Die();

                    GlobalState.SpawnEntity(new Explosion(this));
                }
            }
        }
    }

    public override IEnumerable<Entity> SubEntities()
    {
        return gasClouds.Entities.Concat(base.SubEntities());
    }

    [Collision(typeof(Player), typeof(Broom), typeof(Silverfish), KeepOnScreen = false, MapCollision = false)]
    private class Gas : Entity
    {
        public Gas()
            : base(Vector2.Zero, new AnimatedSpriteRenderer("gas_guy_cloud", 24, 24, new Anim("move", new int[] { 0, 1 }, 3)), DrawOrder.FG_SPRITES)
        {
            offset = new Vector2(4);

            width = height = 16;
        }

        public void Spawn(Entity parent, Entity target)
        {
            Position = parent.Position;

            MoveTowards(target.Position, 30);

            velocity.X += GlobalState.RNG.Next(-10, 11);
            velocity.Y += GlobalState.RNG.Next(-10, 11);

            Flicker(9001);

            opacity = 0.8f;
        }

        public override void Update()
        {
            base.Update();

            opacity -= 0.06f * GameTimes.DeltaTime;

            if (opacity <= 0f)
            {
                exists = false;
            }
        }

        public override void Collided(Entity other)
        {
            if (opacity > 0.3f && other is Player p)
            {
                p.reversed = true;
                GlobalState.wave.active = true;
            }
            else if (other is Broom)
            {
                opacity -= 0.025f;
            }
            else if (other is Silverfish sf)
            {
                sf.GetHit();
            }
        }
    }
}
