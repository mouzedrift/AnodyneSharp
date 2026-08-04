using AnodyneSharp.Entities.Base.Rendering;
using AnodyneSharp.Registry;
using AnodyneSharp.Sounds;
using Microsoft.Xna.Framework;
using RSG;
using System;
using System.Collections.Generic;
using System.Text;

namespace AnodyneSharp.Entities.Enemy.Apartment;

[Collision(typeof(Player), typeof(Dust), KeepOnScreen = true, MapCollision = true)]
public abstract class DashTrap : Entity
{
    protected const float DashVel = 80;

    public const string DamageDealer = "Dash Trap";

    public static AnimatedSpriteRenderer GetSprite()
    {
        if (GlobalState.BoiEaster)
        {
            return new("dash_trap", 16, 16,
                new Anim("idle", new int[] { 6 }, 1),
                new Anim("dash", new int[] { 6 }, 1),
                new Anim("bounce", new int[] { 6 }, 1));
        }
        else
        {
            return new("dash_trap", 16, 16,
                new Anim("idle", new int[] { 4 }, 12),
                new Anim("dash", new int[] { 5 }, 12),
                new Anim("bounce", new int[] { 4,5 }, 12));
        }
    }

    public DashTrap(EntityPreset preset) : base(preset.Position, GetSprite(), Drawing.DrawOrder.ENTITIES)
    {
        width = height = 14;
        CenterOffset();
        MapInteraction = false;
        HoleAsWall = true;
    }

    public override void Collided(Entity other)
    {
        if (other is Player p)
        {
            if (p.state != PlayerState.AIR)
            {
                p.ReceiveDamage(1,DamageDealer);
                OnPlayer();
            }
        }
        else
        {
            OnTouch();
        }
    }

    public override void Update()
    {
        base.Update();
        if (touching != Touching.NONE)
        {
            OnTouch();
        }
    }

    protected abstract void OnTouch();
    protected abstract void OnPlayer();
}

[NamedEntity("Dash_Trap", null, 1, 2)]
public class BounceDashTrap : DashTrap
{
    public BounceDashTrap(EntityPreset preset, Player p) : base(preset)
    {
        Play("dash");
        if (preset.Frame == 1)
        {
            velocity.X = DashVel;
        }
        else
        {
            velocity.Y = DashVel;
        }
    }

    protected override void OnPlayer()
    {
        //Ignored
    }

    protected override void OnTouch()
    {
        Play("bounce");
        SoundManager.PlaySoundEffect("shieldy_ineffective");
        velocity = -velocity;
    }
}

[NamedEntity("Dash_Trap", null, 0), Collision(typeof(DashTrap))]
public class OnSightDashTrap : DashTrap
{
    private enum TrapState
    {
        Idle,
        Charging,
        Returning
    }

    private TrapState _state;

    private Player _player;
    private Vector2 _idlepos;

    public OnSightDashTrap(EntityPreset preset, Player p) : base(preset)
    {
        _player = p;
        _idlepos = Position;

        ChangeState(TrapState.Idle);
    }

    private void ChangeState(TrapState newState)
    {
        _state = newState;

        switch (_state)
        {
            case TrapState.Idle:
                Play("idle");
                break;

            case TrapState.Charging:
                FaceTowards(_player.Center);
                velocity = FacingDirection(facing) * DashVel;
                SoundManager.PlaySoundEffect("slasher_atk");
                Play("dash");
                break;

            case TrapState.Returning:
                break;
        }
    }

    private void Bounce()
    {
        if (_state != TrapState.Charging)
        {
            return;
        }

        SoundManager.PlaySoundEffect("shieldy_ineffective");
        velocity = -velocity / 2;
        ChangeState(TrapState.Returning);
    }

    public override void Update()
    {
        base.Update();

        switch (_state)
        {
            case TrapState.Idle:
                if (SeePlayer())
                {
                    ChangeState(TrapState.Charging);
                }
                break;

            case TrapState.Returning:
                if ((_idlepos - Position).LengthSquared() < 4)
                {
                    velocity = Vector2.Zero;
                    Position = _idlepos;
                    ChangeState(TrapState.Idle);
                }
                break;
        }
    }

    bool SeePlayer()
    {
        bool seeX = _player.Hitbox.Left <= Hitbox.Right && _player.Hitbox.Right >= Hitbox.Left;
        bool seeY = _player.Hitbox.Top <= Hitbox.Bottom && _player.Hitbox.Bottom >= Hitbox.Top;
        return seeX || seeY;
    }

    protected override void OnPlayer()
    {
        Bounce();
    }

    protected override void OnTouch()
    {
        Bounce();
    }
}
