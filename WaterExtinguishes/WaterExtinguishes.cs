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
// Water Extinguishes
// by EristeYasar
private static bool WATER_EXTINGUISHES = true;
private static bool ACID_EXTINGUISHES = true;
public static void OnStartup()
{
    Events.UpdateCallback.Start(OnUpdate);
}
public static void OnUpdate(float delta_ms)
{
    foreach (FireNode fireNode in Game.GetFireNodes())
    {
        if (!((IsInWater(fireNode.Position) && WATER_EXTINGUISHES) || (IsInAcid(fireNode.Position) && ACID_EXTINGUISHES))) {continue;}
        Game.EndFireNode(fireNode.InstanceID);
    }
    foreach (IObject burningObj in Game.GetBurningObjects())
    {
        if (!((IsInWater(burningObj.GetWorldPosition()) && WATER_EXTINGUISHES) || (IsInAcid(burningObj.GetWorldPosition()) && ACID_EXTINGUISHES))) {continue;}
        Game.PlayEffect("TR_SPR", burningObj.GetWorldPosition(), burningObj.UniqueID, "TR_S", 3f);
        burningObj.ClearFire();
    }
}
private static bool IsInWater(Vector2 pos)
{
    return Game.GetObjectsByName("WaterZone").Any(waterZone => waterZone.GetAABB().Contains(pos));
}
private static bool IsInAcid(Vector2 pos)
{
    return Game.GetObjectsByName("AcidZone").Any(acidZone => acidZone.GetAABB().Contains(pos));
}
/* SCRIPT ENDS HERE - COPY ABOVE INTO THE SCRIPT WINDOW */
}
}