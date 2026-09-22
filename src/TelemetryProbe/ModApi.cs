using HarmonyLib;
using System;

namespace DonChan.TelemetryProbe
{
    public sealed class ModApi : IModApi
    {
        public void InitMod(Mod modInstance)
        {
            try
            {
                TelemetryRuntime.Initialize(modInstance);
                ProbeLog.Info("Init complete: " + modInstance.Name);
            }
            catch (Exception ex)
            {
                Log.Error("[DonChanTelemetryProbe] Init failed: " + ex);
                throw;
            }
        }
    }
}
