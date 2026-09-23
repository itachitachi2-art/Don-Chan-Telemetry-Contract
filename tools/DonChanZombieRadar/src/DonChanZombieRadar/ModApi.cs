using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DonChan.ZombieRadar
{
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            try
            {
                RadarRuntime.Initialize(modInstance);
                PatchDeaths();
                Log.Out("[DonChanZombieRadar] Loaded v0.1.1");
            }
            catch (Exception ex)
            {
                Log.Error("[DonChanZombieRadar] Init failed: " + ex);
                throw;
            }
        }

        private static void PatchDeaths()
        {
            Harmony harmony = new Harmony("itachi.donchan.zombieradar");
            MethodInfo postfix = typeof(ModApi).GetMethod("DeathPostfix", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo[] methods = typeof(EntityAlive).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int patched = 0;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "OnEntityDeath" || method.IsAbstract || method.ContainsGenericParameters) continue;
                try
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    patched++;
                }
                catch (Exception ex)
                {
                    Log.Warning("[DonChanZombieRadar] OnEntityDeath patch skipped: " + ex.Message);
                }
            }
            Log.Out("[DonChanZombieRadar] OnEntityDeath patches: " + patched);
        }

        private static void DeathPostfix(EntityAlive __instance)
        {
            RadarRuntime.RecordDeath(__instance);
        }
    }
}
