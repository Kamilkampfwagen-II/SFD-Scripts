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
const int VFX_TIME_MS = 50;
const float SCREEN_SHAKE_TIME = 200;
const float SCREEN_SHAKE_INTENSITY = 10f;
private static readonly Vector2 FOOT_HEAD_OFFSET = new Vector2(0,14);
private static readonly Vector2 CROUCH_HEAD_OFFSET = new Vector2(0,10);
private static readonly Vector2 ROLL_HEAD_OFFSET = new Vector2(0,3);
public static readonly string[] AOE_DESTROY_LIST = {"Balloon", "Beachball", "Computer00", "Monitor", "CardboardBox", "GrabCan", "Cup", "DrinkingGlass", "Lamp00", "Paper", "Light", "GlassSh", "Parrot"};
public static readonly string[] AOE_IGNORE_LIST = {"Bg", "_D", "Weak", "Debris", "Warning"};
public static readonly string[] RAYCAST_IGNORE_LIST = {"Glass", "Invisible"};

public static void OnStartup()
{
    Events.ObjectCreatedCallback.Start(OnObjectCreated);
    Events.PlayerKeyInputCallback.Start(OnPlayerKeyInput);
}
// Triggered by Event Listeners:
public static void OnObjectCreated(IObject[] obj_list)
{
    foreach (IObject obj in obj_list)
    {
        if (obj.Name != "WpnGrenadesThrown") {continue;}
        IObjectGrenadeThrown grenadeThrown = (IObjectGrenadeThrown)obj;

        grenadeThrown.SetExplosionTimer(FUSE_TIME_MS);
        grenadeThrown.SetDudChance(100);

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
        grenadeThrown.SetCollisionFilter(collisionFilter);

        RunTimer(FUSE_TIME_MS, "Detonate", grenadeThrown.UniqueID.ToString());
    }
}
public static void OnPlayerKeyInput(IPlayer ply, VirtualKeyInfo[] keyInfo_list)
{
    if (!keyInfo_list.Any(keyInfo => keyInfo.Key == VirtualKey.ATTACK)) {return;}
    if (!ply.IsHoldingActiveThrowable) {return;}
    if (ply.GetActiveThrowableWeaponItem() != WeaponItem.GRENADES) {return;}

    ply.SetActiveThrowableTimer(COOK_TIME_MS);
}
// Triggered by Timer Triggers:
public static void Detonate(TriggerArgs args)
{
    IObject timerTrigger = (IObject)args.Caller;
    timerTrigger.Remove();

    IObject grenadeThrown = Game.GetObject(int.Parse(timerTrigger.CustomID));
    if (grenadeThrown == null) {return;}
    grenadeThrown.Remove();

    Vector2 here = grenadeThrown.GetWorldPosition();

    string effectTile_customID = string.Format("EffectTile{0}", grenadeThrown.UniqueID);
    PlayFX(here, effectTile_customID);

    AreaEffect(new Area(here - new Vector2(MAX_AOE_RANGE,MAX_AOE_RANGE), here + new Vector2(MAX_AOE_RANGE,MAX_AOE_RANGE)));

    foreach (IPlayer ply in Game.GetPlayers())
    {
        if (ply.IsDead) {continue;}
        if (ply.IsBlocking) {continue;}

        Vector2 ply_headPos = GetPlayerHeadPosition(ply);

        float distance = (here - ply_headPos).Length();
        if (distance > MAX_FLASH_RANGE) {continue;}

        RayCastInput rayCastInput = new RayCastInput() {IncludeOverlap = true, FilterOnMaskBits = true, MaskBits = 0xFFFF};
        RayCastResult[] rayCastResult_list = Game.RayCast(here, ply_headPos, rayCastInput);
        bool isHit = AnalyzeRayCastResults(rayCastResult_list, ply);
        if (!isHit) {continue;}

        bool isFacing = false;
        if (here.X > ply_headPos.X && ply.FacingDirection == 1) {isFacing = true;}
        if (here.X < ply_headPos.X && ply.FacingDirection == -1) {isFacing = true;}

        float time = Map(MAX_FLASH_RANGE - distance, 0, MAX_FLASH_RANGE - MIN_FLASH_RANGE, 0, MAX_FLASH_TIME_MS);

        Flash(ply, (int)time, isFacing);
    }
}
public static void RemoveVFX(TriggerArgs args)
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

    IPlayer ply = Game.GetPlayer(int.Parse(timerTrigger.CustomID));
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
        if (!AOE_DESTROY_LIST.Any(obj.Name.Contains)) {continue;}
        if (AOE_IGNORE_LIST.Any(obj.Name.Contains)) {continue;}
        obj.Destroy();
    }
}
public static Vector2 GetPlayerHeadPosition(IPlayer ply) // Needs improvement
{
    Vector2 ply_headOffset;
    if (ply.IsCrouching || ply.IsLedgeGrabbing)
    {
        ply_headOffset = CROUCH_HEAD_OFFSET;
    }
    else if (ply.IsRolling)
    {
        ply_headOffset = ROLL_HEAD_OFFSET;
    }
    else if (ply.IsDiving)
    {
        ply_headOffset = ROLL_HEAD_OFFSET;
    }
    else
    {
        ply_headOffset = FOOT_HEAD_OFFSET;
    }
    return ply.GetWorldPosition() + ply_headOffset;
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
private static void PlayFX(Vector2 here, string customID)
{
    // White light tile from SFR:
    for (int index = 1; index < 10; index++)
    {
        // Game.CreateObject("FgLightRadiusWhite03A", here).CustomID = customID;
        Game.CreateObject("BgLightRadiusWhite03A", here).CustomID = customID; // Temporary fix for SFR 1.0.3c update
    }

    // Particles:
    Game.PlayEffect("DestroyMetal", here);  // Metal
    Game.PlayEffect("S_P", here);           // Sparks
    Game.PlayEffect("STM", here);           // Steam

    // Camera Shake:
    Game.PlayEffect("CAM_S", here, SCREEN_SHAKE_INTENSITY, SCREEN_SHAKE_TIME, true);

    // SFX:
    Game.PlaySound("Explosion", here);
    Game.PlaySound("Splash", here);

    RunTimer(VFX_TIME_MS, "RemoveVFX", customID);
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