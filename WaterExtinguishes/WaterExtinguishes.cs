using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SFDGameScriptInterface;

namespace WaterExtinguishes {
public partial class GameScript : GameScriptInterface
{
public GameScript() : base(null) {}
/* SCRIPT STARTS HERE - COPY BELOW INTO THE SCRIPT WINDOW */
// Water Extinguishes
// by EristeYasar

// Settings:
const bool WATER_EXTINGUISHES = true;
const bool ACID_EXTINGUISHES = true;
const float SMOKE_TIME_S = 3f;

public static void OnStartup()
{
    Events.UpdateCallback.Start(OnUpdate);
}
public static void OnUpdate(float delta_ms)
{
    foreach (FireNode fireNode in Game.GetFireNodes())
    {
        if (!((IsInWaterZone(fireNode.Position) && WATER_EXTINGUISHES) || (IsInAcidZone(fireNode.Position) && ACID_EXTINGUISHES))) {continue;}
        Game.PlaySound("Splash", fireNode.Position);
        Game.EndFireNode(fireNode.InstanceID);
    }

    foreach (IObject burningObj in Game.GetBurningObjects())
    {
        if (!((IsInWaterZone(burningObj.GetWorldPosition()) && WATER_EXTINGUISHES) || (IsInAcidZone(burningObj.GetWorldPosition()) && ACID_EXTINGUISHES))) {continue;}
        Game.PlayEffect("TR_SPR", burningObj.GetWorldPosition(), burningObj.UniqueID, "TR_S", SMOKE_TIME_S);
        Game.PlaySound("Splash", burningObj.GetWorldPosition());
        burningObj.ClearFire();
    }
}
private static bool IsInWaterZone(Vector2 pos)
{
    return Game.GetObjectsByName("WaterZone").Any(waterZone => waterZone.GetAABB().Contains(pos));
}
private static bool IsInAcidZone(Vector2 pos)
{
    return Game.GetObjectsByName("AcidZone").Any(acidZone => acidZone.GetAABB().Contains(pos));
}
/* SCRIPT ENDS HERE - COPY ABOVE INTO THE SCRIPT WINDOW */
}
}
