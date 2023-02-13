using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SFDGameScriptInterface;

namespace Flashbang {
public partial class GameScript : GameScriptInterface
{
public GameScript() : base(null) {}
/* SCRIPT STARTS HERE - COPY BELOW INTO THE SCRIPT WINDOW */
// Flashbang
// by EristeYasar

// Settings:
const int COOK_TIME_MS = 3600000;
const int FUSE_TIME_MS = 2300;
const int MAX_AOE_RANGE = 100;
const int MIN_FLASH_RANGE = 20;
const int MAX_FLASH_RANGE = 100;
const int MAX_FLASH_TIME_MS = 1750;
const int EFFECT_TIME_MS = 50;
const float SCREEN_SHAKE_INTENSITY = 10f;
private static readonly Vector2 FOOT_HEAD_OFFSET = new Vector2(0,14);
private static readonly Vector2 CROUCH_HEAD_OFFSET = new Vector2(0,10);
private static readonly Vector2 ROLL_HEAD_OFFSET = new Vector2(0,3);
public static readonly string[] AOE_BREAK_LIST = {"Balloon", "Beachball", "Computer00", "Monitor", "CardboardBox", "GrabCan", "Cup", "DrinkingGlass", "Lamp00", "Paper", "Light", "GlassSh", "Parrot"};
public static readonly string[] AOE_BREAK_IGNORE_LIST = {"Bg", "_D", "Weak", "Debris", "Warning"};
public static readonly string[] RAYCAST_IGNORE_LIST = {"Glass", "Invisible"};

public static void OnStartup()
{
    Events.ObjectCreatedCallback.Start(OnObjectCreated);
    Events.PlayerKeyInputCallback.Start(OnPlayerKeyInput);
}
// Triggered by Event Listeners:
public static void OnObjectCreated(IObject[] obj_list)
{
    foreach (IObjectGrenadeThrown grenade_thrown in obj_list.Where(obj => obj.Name == "WpnGrenadesThrown"))
    {
        grenade_thrown.SetExplosionTimer(FUSE_TIME_MS);
        grenade_thrown.SetDudChance(100);

        CollisionFilter collisionFilter = new CollisionFilter
        {
            AboveBits = 0,
            AbsorbProjectile = false,
            BlockExplosions = false,
            BlockFire = false,
            BlockMelee = false,
            CategoryBits = 32768,
            MaskBits = 32779,
            ProjectileHit = false
        };
        grenade_thrown.SetCollisionFilter(collisionFilter);

        RunTimer(FUSE_TIME_MS, "Detonate", grenade_thrown.UniqueID.ToString());
    }
}
public static void OnPlayerKeyInput(IPlayer ply, VirtualKeyInfo[] keyInfo_list)
{
    if (keyInfo_list.Any(keyInfo => keyInfo.Key == VirtualKey.ATTACK)) {return;}
    if (!ply.IsHoldingActiveThrowable) {return;}
    if (ply.GetActiveThrowableWeaponItem() != WeaponItem.GRENADES) {return;}

    ply.SetActiveThrowableTimer(COOK_TIME_MS);
}
// Triggered by Timer Triggers:
public static void Detonate(TriggerArgs args)
{
    IObject timerTrigger = (IObject)args.Caller;
    timerTrigger.Remove();

    IObject grenade_thrown = Game.GetObject(Int32.Parse(timerTrigger.CustomID));
    if (grenade_thrown == null) {return;}
    grenade_thrown.Remove();

    Vector2 here = grenade_thrown.GetWorldPosition();
    Area aoe_obj = new Area(here - new Vector2(MAX_AOE_RANGE,MAX_AOE_RANGE), here + new Vector2(MAX_AOE_RANGE,MAX_AOE_RANGE));
    AreaEffect(aoe_obj);

    string effectTile_customID = string.Format("EffectTile{0}", grenade_thrown.UniqueID);
    PlayEffect(here, effectTile_customID);

    foreach (IPlayer ply in Game.GetPlayers().Where(ply => !ply.IsDead))
    {
        if (ply.IsBlocking) {continue;}

        Vector2 head_offset;
        if (ply.IsCrouching || ply.IsLedgeGrabbing)
        {
            head_offset = CROUCH_HEAD_OFFSET;
        }
        else if (ply.IsRolling)
        {
            head_offset = ROLL_HEAD_OFFSET;
        }
        else if (ply.IsDiving)
        {
            head_offset = ROLL_HEAD_OFFSET;
        }
        else
        {
            head_offset = FOOT_HEAD_OFFSET;
        }
        Vector2 ply_head_pos = ply.GetWorldPosition() + head_offset;

        float distance = (here - ply_head_pos).Length();
        if (distance > MAX_FLASH_RANGE) {continue;}

        RayCastInput rayCastInput = new RayCastInput() {IncludeOverlap = true, FilterOnMaskBits = true, MaskBits = 0xFFFF};
        RayCastResult[] rayCastResult_list = Game.RayCast(here, ply_head_pos, rayCastInput);
        bool isHit = AnalyzeRayCastResults(rayCastResult_list, ply);
        if (!isHit) {continue;}

        bool isFacing = false;
        if (here.X > ply_head_pos.X && ply.FacingDirection == 1) {isFacing = true;}
        if (here.X < ply_head_pos.X && ply.FacingDirection == -1) {isFacing = true;}

        float time = Map(MAX_FLASH_RANGE - distance, 0, MAX_FLASH_RANGE - MIN_FLASH_RANGE, 0, MAX_FLASH_TIME_MS);
        Flash(ply, (int)time, isFacing);
    }
}
public static void RemoveEffect(TriggerArgs args)
{
    IObject timerTrigger = (IObject)args.Caller;
    timerTrigger.Remove();

    foreach (IObject obj in Game.GetObjectsByCustomID(timerTrigger.CustomID))
    {
        obj.Remove();
    }
}
public static void UnFlash(TriggerArgs args)
{
    IObject timerTrigger = (IObject)args.Caller;
    timerTrigger.Remove();

    IPlayer ply = Game.GetPlayer(Int32.Parse(timerTrigger.CustomID));
    if (ply == null) {return;}

    ply.AddCommand(new PlayerCommand(PlayerCommandType.StopStagger));
    ply.SetInputEnabled(true);
}
// Triggered by other methods:
private static void AreaEffect(Area AOE)
{
    foreach (IObject obj in Game.GetObjectsByArea(AOE, PhysicsLayer.Active))
    {
        if ((obj.GetWorldPosition() - AOE.Center).Length() > MAX_AOE_RANGE) {continue;}
        if (!AOE_BREAK_LIST.Any(obj.Name.Contains)) {continue;}
        if (AOE_BREAK_IGNORE_LIST.Any(obj.Name.Contains)) {continue;}
        obj.Destroy();
    }
}
private static bool AnalyzeRayCastResults(RayCastResult[] rayCastResult_list, IObject obj)
{
    bool isHit = false;
    foreach (RayCastResult result in rayCastResult_list)
    {
        if (RAYCAST_IGNORE_LIST.Any(result.HitObject.Name.Contains)) {continue;}
        if (result.HitObject.GetCollisionFilter().AbsorbProjectile == true && !result.IsPlayer) {break;}
        if (result.HitObject != obj) {continue;}
        isHit = true;
    }
    return isHit;
}
private static void Flash(IPlayer ply, int time, bool isFacing)
{
    ply.Disarm(ply.CurrentWeaponDrawn);

    if (isFacing)
    {
        Gender ply_gender = ply.GetProfile().Gender;
        switch (ply_gender)
        {
            case Gender.Male:
                Game.PlaySound("Wilhelm", ply.GetWorldPosition());
                break;
            case Gender.Female:
                Game.PlaySound("CartoonScream", ply.GetWorldPosition());
                break;
        }

        ply.SetInputEnabled(false);
        ply.AddCommand(new PlayerCommand(PlayerCommandType.StaggerInfinite));
        RunTimer(time, "UnFlash", ply.UniqueID.ToString());
    }
    else
    {
        ply.SetInputEnabled(false);
        ply.AddCommand(new PlayerCommand(PlayerCommandType.Fall));
        RunTimer(500, "UnFlash", ply.UniqueID.ToString());
    }
}
private static void PlayEffect(Vector2 here, string customID)
{
    // White light tile from SFR:
    for (int index = 1; index < 10; index++)
    {
        Game.CreateObject("FgLightRadiusWhite03A", here).CustomID = customID;
        Game.CreateObject("BgLightRadiusWhite03A", here).CustomID = customID;
    }

    // Camera Shake:
    Game.PlayEffect("CAM_S", here, SCREEN_SHAKE_INTENSITY, (float)EFFECT_TIME_MS*4, true);

    // Particles:
    Game.PlayEffect("DestroyMetal", here);  // Metal
    Game.PlayEffect("S_P", here);           // Sparks
    Game.PlayEffect("STM", here);           // Steam

    // SFX:
    Game.PlaySound("Explosion", here);
    Game.PlaySound("Splash", here);

    RunTimer(EFFECT_TIME_MS, "RemoveEffect", customID);
}
private static void RunTimer(int intervalTime_ms, string scriptMethod, string customID = "", int repeatCount = 1)
{
    IObjectTimerTrigger timerTrigger = (IObjectTimerTrigger)Game.CreateObject("TimerTrigger");
    timerTrigger.SetIntervalTime(intervalTime_ms);
    timerTrigger.SetScriptMethod(scriptMethod);
    timerTrigger.CustomID = customID;
    timerTrigger.SetRepeatCount(repeatCount);
    timerTrigger.Trigger();
}
private static float Map(float value, float sourceLow, float sourceHigh, float targetLow, float targetHigh) 
{
    return (value - sourceLow) / (sourceHigh - sourceLow) * (targetHigh - targetLow) + targetLow;
}
/* SCRIPT ENDS HERE - COPY ABOVE INTO THE SCRIPT WINDOW */
}
}
