﻿
using Multitool.Interop.Codes;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Multitool.Interop.Power
{
    public class PowerOptions
    {
        public event PowerPlanChangedEventHandler PowerPlanChanged;
        internal static event PowerPlanChangedEventHandler ActiveChanged;

        /// <summary>
        /// Get this computer current power plan.
        /// </summary>
        public PowerPlan GetActivePowerPlan()
        {
            IntPtr guid = IntPtr.Zero;
            uint returnCode = PowerGetActiveScheme(IntPtr.Zero, ref guid);
            if (returnCode != (uint)SystemCodes.ERROR_SUCCESS)
            {
                throw InteropHelper.GetLastError("PowerGetActiveScheme call failed", returnCode);
            }
            string name = ReadFriendlyName(guid);
            if (name == string.Empty)
            {
                throw InteropHelper.GetLastError("PowerGetActiveScheme call failed. Name buffer was empty.", returnCode);
            }
            else
            {
                return new PowerPlan(Marshal.PtrToStructure<Guid>(guid), name, true);
            }
        }

        public Guid GetActivePowerPlanGuid()
        {
            IntPtr guid = IntPtr.Zero;
            uint returnCode = PowerGetActiveScheme(IntPtr.Zero, ref guid);
            if (returnCode != (uint)SystemCodes.ERROR_SUCCESS)
            {
                throw InteropHelper.GetLastError("PowerGetActiveScheme call failed", returnCode);
            }
            return Marshal.PtrToStructure<Guid>(guid);
        }

        /// <summary>
        /// Get this computer available power plans.
        /// </summary>
        /// <returns><see cref="List{PowerPlan}"/> of power plans names.</returns>
        public PowerPlan[] EnumeratePowerPlans()
        {
            List<Guid> guids = ListPowerPlans();
            PowerPlan[] powerPlans = new PowerPlan[guids.Count];
            PowerPlan current = GetActivePowerPlan();
            Guid guid;
            for (int i = 0; i < guids.Count; i++)
            {
                guid = guids[i];
                PowerPlan plan = new(guid, ReadFriendlyName(ref guid), guid == current.Guid);
                powerPlans[i] = plan;
            }
            return powerPlans;
        }

        public void SetActivePowerPlan(PowerPlan powerPlan)
        {
            Guid guid = powerPlan.Guid;
            uint retCode = PowerSetActiveScheme(IntPtr.Zero, ref guid);

            ActiveChanged?.Invoke(powerPlan.Guid);
            if (retCode != (uint)SystemCodes.ERROR_SUCCESS)
            {
                throw InteropHelper.GetLastError("PowerSetActiveScheme call failed", retCode);
            }
            else
            {
                _ = PowerPlanChanged?.BeginInvoke(powerPlan.Guid, null, null);
            }
        }

        #region private
        private static List<Guid> ListPowerPlans()
        {
            List<Guid> guids = new(3);
            IntPtr buffer;
            uint index = 0;
            uint returnCode = 0;
            uint bufferSize = 16;

            while (returnCode == 0)
            {
                buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    returnCode = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME,
                                                index, buffer, ref bufferSize);
                    if (returnCode == 259)
                    {
                        break;
                    }
                    else if (returnCode != 0)
                    {
                        throw InteropHelper.GetLastError("Error while listing power schemes.", returnCode);
                    }
                    else
                    {
                        try
                        {
                            Guid guid = Marshal.PtrToStructure<Guid>(buffer);
                            guids.Add(guid);
                        }
                        catch (ArgumentException) { }
                    }
                    index++;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return guids;
        }

        private static string ReadFriendlyName(IntPtr schemeGuid)
        {
            uint bufferSize = 0;
            uint returnCode = PowerReadFriendlyName(IntPtr.Zero, schemeGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);
            if (returnCode == 0)
            {
                if (bufferSize == 0)
                {
                    return string.Empty;
                }

                IntPtr namePtr = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    returnCode = PowerReadFriendlyName(IntPtr.Zero, schemeGuid, IntPtr.Zero, IntPtr.Zero, namePtr, ref bufferSize);
                    if (returnCode == 0)
                    {
                        string name = Marshal.PtrToStringUni(namePtr);
                        return name;
                    }
                    else
                    {
                        throw InteropHelper.GetLastError("Error getting power scheme friendly name.", returnCode);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(namePtr);
                }
            }
            else
            {
                throw InteropHelper.GetLastError("Error getting name buffer size", returnCode);
            }
        }

        private static string ReadFriendlyName(ref Guid guid)
        {
            uint bufferSize = 0;
            uint returnCode = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);

            if (returnCode == 0)
            {
                if (bufferSize == 0)
                {
                    return string.Empty;
                }

                IntPtr namePtr = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    returnCode = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, namePtr, ref bufferSize);

                    if (returnCode == 0)
                    {
                        string name = Marshal.PtrToStringUni(namePtr);
                        return name;
                    }
                    else
                    {
                        throw InteropHelper.GetLastError("Error getting power scheme friendly name.", returnCode);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(namePtr);
                }
            }
            else
            {
                throw InteropHelper.GetLastError("Error getting name buffer size", returnCode);
            }
        }
        #endregion

        #region dllimports
        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerEnumerate(
            IntPtr RootPowerKey,
            IntPtr SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            uint AccessFlags,
            uint Index,
            IntPtr Buffer,
            ref uint BufferSize
        );

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadFriendlyName(
            IntPtr RootPowerKey,
            IntPtr SchemeGuid,
            IntPtr SubGroupOfPowerSettingGuid,
            IntPtr PowerSettingGuid,
            IntPtr Buffer,
            ref uint BufferSize
        );

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadFriendlyName(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            IntPtr SubGroupOfPowerSettingGuid,
            IntPtr PowerSettingGuid,
            IntPtr Buffer,
            ref uint BufferSize
        );

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(
            IntPtr UserRootPowerKey,
            ref IntPtr ActivePolicyGuid
        );

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerSetActiveScheme(
            IntPtr UserRootPowerKey,
            ref Guid SchemeGuid
        );

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam
        );
        #endregion
    }

    // This structure is sent when the PBT_POWERSETTINGSCHANGE message is sent.
    // It describes the power setting that has changed and contains data about the change
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }
}
