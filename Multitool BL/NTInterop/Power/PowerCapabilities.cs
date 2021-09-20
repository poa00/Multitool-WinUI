﻿using Multitool.NTInterop.Structs;

using System.Runtime.InteropServices;

namespace Multitool.NTInterop.Power
{
    public class PowerCapabilities
    {
        #region DllImports
        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool GetPwrCapabilities(out SYSTEM_POWER_CAPABILITIES sysCaps);
        #endregion

        public PowerCapabilities()
        {
            GetPowerCapabilities();
            CpuStates = GetCpuPowerStates();
        }

        #region properties
        public bool PowerButtonPresent { get; private set; }
        public bool SleepButtonPresent { get; private set; }
        public bool LidPresent { get; private set; }
        public CpuPowerStates CpuStates { get; private set; }
        public bool S1 { get; private set; }
        public bool S2 { get; private set; }
        public bool S3 { get; private set; }
        public bool S4 { get; private set; }
        public bool S5 { get; private set; }
        public byte ProcessorMaxThrottle { get; private set; }
        public byte ProcessorMinThrottle { get; private set; }
        public bool ProcessorThrottle { get; private set; }
        public bool SystemBatteriesPresent { get; private set; }
        public bool BatteriesAreShortTerm { get; private set; }
        public BatteryReportingScale BatterieScale1 { get; private set; }
        public BatteryReportingScale BatterieScale2 { get; private set; }
        public BatteryReportingScale BatterieScale3 { get; private set; }
        public bool HibernationFilePresent { get; private set; }
        public bool FullWake { get; private set; }
        public bool VideoDimPresent { get; private set; }
        public bool ApmPresent { get; private set; }
        public bool UpsPresent { get; private set; }
        public bool ThermalControl { get; private set; }
        public bool DiskSpinDown { get; private set; }
        public SystemPowerState AcOnLineWake { get; private set; }
        public SystemPowerState SoftLidWake { get; private set; }
        public SystemPowerState RtcWake { get; private set; }
        public SystemPowerState MinDeviceWakeState { get; private set; }
        public SystemPowerState DefaultLowLatencyWake { get; private set; }
        #endregion

        public static bool IsHibernationAllowed()
        {
            if (GetPwrCapabilities(out SYSTEM_POWER_CAPABILITIES sys))
            {
                return sys.SystemS4;
            }
            else
            {
                throw InteropHelper.GetLastError("GetPwrCapabilities returned zero code");
            }
        }

        #region private methods

        private void GetPowerCapabilities()
        {
            if (GetPwrCapabilities(out SYSTEM_POWER_CAPABILITIES sys))
            {
                PowerButtonPresent = sys.PowerButtonPresent;
                SleepButtonPresent = sys.SleepButtonPresent;
                LidPresent = sys.LidPresent;
                S1 = sys.SystemS1;
                S2 = sys.SystemS2;
                S3 = sys.SystemS3;
                S4 = sys.SystemS4;
                S5 = sys.SystemS5;
                ProcessorMaxThrottle = sys.ProcessorMaxThrottle;
                ProcessorMinThrottle = sys.ProcessorMinThrottle;
                ProcessorThrottle = sys.ProcessorThrottle;
                SystemBatteriesPresent = sys.SystemBatteriesPresent;
                BatteriesAreShortTerm = sys.BatteriesAreShortTerm;
                BatterieScale1 = new BatteryReportingScale(sys.BatteryScale[0].Granularity, sys.BatteryScale[0].Granularity);
                BatterieScale2 = new BatteryReportingScale(sys.BatteryScale[1].Granularity, sys.BatteryScale[1].Granularity);
                BatterieScale3 = new BatteryReportingScale(sys.BatteryScale[2].Granularity, sys.BatteryScale[2].Granularity);
                HibernationFilePresent = sys.HiberFilePresent;
                FullWake = sys.FullWake;
                VideoDimPresent = sys.VideoDimPresent;
                ApmPresent = sys.UpsPresent;
                UpsPresent = sys.UpsPresent;
                ThermalControl = sys.ThermalControl;
                DiskSpinDown = sys.DiskSpinDown;
                AcOnLineWake = new SystemPowerState(sys.AcOnLineWake);
                SoftLidWake = new SystemPowerState(sys.SoftLidWake);
                RtcWake = new SystemPowerState(sys.RtcWake);
                MinDeviceWakeState = new SystemPowerState(sys.MinDeviceWakeState);
                DefaultLowLatencyWake = new SystemPowerState(sys.DefaultLowLatencyWake);
            }
            else
            {
                throw InteropHelper.GetLastError(nameof(GetPwrCapabilities) + " failed", 0);
            }
        }

        private CpuPowerStates GetCpuPowerStates()
        {
            CpuPowerStates states = CpuPowerStates.Default;
            if (S1)
            {
                states |= CpuPowerStates.S1Supported;
            }
            if (S2)
            {
                states |= CpuPowerStates.S2Supported;
            }
            if (S3)
            {
                states |= CpuPowerStates.S3Supported;
            }
            if (S4)
            {
                states |= CpuPowerStates.S4Supported;
            }
            if (S5)
            {
                states |= CpuPowerStates.S5Supported;
            }
            return states;
        }

        #endregion
    }
}