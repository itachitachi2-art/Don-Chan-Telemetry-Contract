using System;
using DonChan.TelemetryProbe;
class CombatPolicyTests {
 public static void Hit(int _attackerEntityId, string _attackMode) {}
 static void Main() {
 var m=typeof(CombatPolicyTests).GetMethod("Hit");
 if(CombatEventPolicy.IsRealEvent("combat.hit",m,new object[]{171,"Simulate"})) throw new Exception("simulation leaked");
 if(!CombatEventPolicy.IsRealEvent("combat.hit",m,new object[]{171,"RealNoHarvesting"})) throw new Exception("real lost");
 if(!CombatEventPolicy.IsRealEvent("combat.hit",m,new object[]{171,"RealAndHarvesting"})) throw new Exception("harvest lost");
 if(CombatEventPolicy.IsRealEvent("combat.hit",m,new object[]{171,"future"})) throw new Exception("unknown mode accepted");
 if(CombatEventPolicy.IsRealEvent("combat.hit",m,new object[]{171})) throw new Exception("missing mode accepted");
 if(!CombatEventPolicy.IsRealEvent("combat.damage",m,null)) throw new Exception("damage lost");
 Console.WriteLine("Combat policy: 6 assertions passed");
 }
}
