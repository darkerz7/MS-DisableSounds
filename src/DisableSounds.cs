using Microsoft.Extensions.Configuration;
using Sharp.Modules.ClientPreferences.Shared;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.HookParams;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace MS_DisableSounds
{
    public class DisableSounds : IModSharpModule
    {
        public string DisplayName => "DisableSounds";
        public string DisplayAuthor => "DarkerZ[RUS]";

        public DisableSounds(ISharedSystem sharedSystem, string dllPath, string sharpPath, Version version, IConfiguration coreConfiguration, bool hotReload)
        {
            _modules = sharedSystem.GetSharpModuleManager();
            _clients = sharedSystem.GetClientManager();
            _hooks = sharedSystem.GetHookManager();
        }

        private readonly ISharpModuleManager _modules;
        private readonly IClientManager _clients;
        private readonly IHookManager _hooks;

        private IDisposable? _callback;

        private IModSharpModuleInterface<ILocalizerManager>? _localizer;
        private IModSharpModuleInterface<IClientPreference>? _icp;

        private readonly bool[] g_bWeaponSounds = new bool[65];
        private readonly bool[] g_bHitSounds = new bool[65];
        private readonly bool[] g_bFootSteps = new bool[65];

        public bool Init()
        {
            _hooks.EmitSound.InstallHookPre(OnEmitSound);

            _clients.InstallCommandCallback("loud", OnWeaponCommand);
            _clients.InstallCommandCallback("hits", OnHitCommand);
            _clients.InstallCommandCallback("footsteps", OnFootStepCommand);
            return true;
        }

        public void OnAllModulesLoaded()
        {
            GetClientPrefs();
            GetLocalizer()?.LoadLocaleFile("DisableSounds");
        }

        public void OnLibraryConnected(string name)
        {
            if (name.Equals("ClientPreferences")) GetClientPrefs();
        }

        public void OnLibraryDisconnect(string name)
        {
            if (name.Equals("ClientPreferences")) _icp = null;
        }

        private void OnCookieLoad(IGameClient client)
        {
            if (client == null || !client.IsValid || GetClientPrefs() is not { } cp || !cp.IsLoaded(client.SteamId)) return;

            if (cp.GetCookie(client.SteamId, "WeaponSounds") is { } cookie_enabled)
            {
                string sValue = cookie_enabled.GetString();
                if (string.IsNullOrEmpty(sValue) || !Byte.TryParse(sValue, out byte iValue)) iValue = 0;
                if (iValue == 0) g_bWeaponSounds[client.Slot] = false;
                else g_bWeaponSounds[client.Slot] = true;
            }
            else
            {
                cp.SetCookie(client.SteamId, "WeaponSounds", "0");
                g_bWeaponSounds[client.Slot] = false;
            }

            if (cp.GetCookie(client.SteamId, "HitSounds") is { } cookie_enabled2)
            {
                string sValue = cookie_enabled2.GetString();
                if (string.IsNullOrEmpty(sValue) || !Byte.TryParse(sValue, out byte iValue)) iValue = 0;
                if (iValue == 0) g_bHitSounds[client.Slot] = false;
                else g_bHitSounds[client.Slot] = true;
            }
            else
            {
                cp.SetCookie(client.SteamId, "HitSounds", "0");
                g_bHitSounds[client.Slot] = false;
            }

            if (cp.GetCookie(client.SteamId, "FootSteps") is { } cookie_enabled3)
            {
                string sValue = cookie_enabled3.GetString();
                if (string.IsNullOrEmpty(sValue) || !Byte.TryParse(sValue, out byte iValue)) iValue = 0;
                if (iValue == 0) g_bFootSteps[client.Slot] = false;
                else g_bFootSteps[client.Slot] = true;
            }
            else
            {
                cp.SetCookie(client.SteamId, "FootSteps", "0");
                g_bFootSteps[client.Slot] = false;
            }
        }

        public void Shutdown()
        {
            _hooks.EmitSound.RemoveHookPre(OnEmitSound);
            _clients.RemoveCommandCallback("loud", OnWeaponCommand);
            _clients.RemoveCommandCallback("hits", OnHitCommand);
            _clients.RemoveCommandCallback("footsteps", OnFootStepCommand);
            _callback?.Dispose();
        }

        private HookReturnValue<SoundOpEventGuid> OnEmitSound(IEmitSoundHookParams param, HookReturnValue<SoundOpEventGuid> previousResult)
        {
            if (previousResult.Action is EHookAction.SkipCallReturnOverride) return default;

            for (int i = 0; i < FootStepsArray.Length; i++)
                if (param.SoundName.Equals(FootStepsArray[i]))
                {
                    param.UpdateReceiver(new NetworkReceiver([.. _clients.GetGameClients(true).Where(x => !g_bFootSteps[x.Slot]).Select(x => x.Slot)]));
                    return new HookReturnValue<SoundOpEventGuid>(EHookAction.ChangeParamReturnDefault);
                }

            for (int i = 0; i < HitSoundsArray.Length; i++)
                if (param.SoundName.Equals(HitSoundsArray[i]))
                {
                    param.UpdateReceiver(new NetworkReceiver([.. _clients.GetGameClients(true).Where(x => !g_bHitSounds[x.Slot]).Select(x => x.Slot)]));
                    return new HookReturnValue<SoundOpEventGuid>(EHookAction.ChangeParamReturnDefault);
                }

            for (int i = 0; i < WeaponSoundsArray.Length; i++)
                if (param.SoundName.Equals(WeaponSoundsArray[i]))
                {
                    param.UpdateReceiver(new NetworkReceiver([.. _clients.GetGameClients(true).Where(x => !g_bWeaponSounds[x.Slot]).Select(x => x.Slot)]));
                    return new HookReturnValue<SoundOpEventGuid>(EHookAction.ChangeParamReturnDefault);
                }

            return new HookReturnValue<SoundOpEventGuid>();
        }

        private ECommandAction OnWeaponCommand(IGameClient client, StringCommand command)
        {
            if (client == null || !client.IsValid) return ECommandAction.Stopped;
            g_bWeaponSounds[client.Slot] = !g_bWeaponSounds[client.Slot];
            if (GetClientPrefs() is { } cp && cp.IsLoaded(client.SteamId))
            {
                cp.SetCookie(client.SteamId, "WeaponSounds", g_bWeaponSounds[client.Slot] ? "1" : "0");
            }
            if (client.GetPlayerController() is { } player && GetLocalizer() is { } lm)
            {
                var localizer = lm.GetLocalizer(client);
                player.Print(command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console, $" {ChatColor.Blue}[{ChatColor.Green}DisableSounds{ChatColor.Blue}]{ChatColor.White} {ReplaceColorTags(g_bWeaponSounds[client.Slot] ? localizer.Format("WeaponSounds.Disabled") : localizer.Format("WeaponSounds.Enabled"))}");
            }
            return ECommandAction.Stopped;
        }

        private ECommandAction OnHitCommand(IGameClient client, StringCommand command)
        {
            if (client == null || !client.IsValid) return ECommandAction.Stopped;
            g_bHitSounds[client.Slot] = !g_bHitSounds[client.Slot];
            if (GetClientPrefs() is { } cp && cp.IsLoaded(client.SteamId))
            {
                cp.SetCookie(client.SteamId, "HitSounds", g_bHitSounds[client.Slot] ? "1" : "0");
            }
            if (client.GetPlayerController() is { } player && GetLocalizer() is { } lm)
            {
                var localizer = lm.GetLocalizer(client);
                player.Print(command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console, $" {ChatColor.Blue}[{ChatColor.Green}DisableSounds{ChatColor.Blue}]{ChatColor.White} {ReplaceColorTags(g_bHitSounds[client.Slot] ? localizer.Format("HitSounds.Disabled") : localizer.Format("HitSounds.Enabled"))}");
            }
            return ECommandAction.Stopped;
        }

        private ECommandAction OnFootStepCommand(IGameClient client, StringCommand command)
        {
            if (client == null || !client.IsValid) return ECommandAction.Stopped;
            g_bFootSteps[client.Slot] = !g_bFootSteps[client.Slot];
            if (GetClientPrefs() is { } cp && cp.IsLoaded(client.SteamId))
            {
                cp.SetCookie(client.SteamId, "FootSteps", g_bFootSteps[client.Slot] ? "1" : "0");
            }
            if (client.GetPlayerController() is { } player && GetLocalizer() is { } lm)
            {
                var localizer = lm.GetLocalizer(client);
                player.Print(command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console, $" {ChatColor.Blue}[{ChatColor.Green}DisableSounds{ChatColor.Blue}]{ChatColor.White} {ReplaceColorTags(g_bFootSteps[client.Slot] ? localizer.Format("FootSteps.Disabled") : localizer.Format("FootSteps.Enabled"))}");
            }
            return ECommandAction.Stopped;
        }

        private string ReplaceColorTags(string input)
        {
            for (var i = 0; i < colorPatterns.Length; i++)
                input = input.Replace(colorPatterns[i], colorReplacements[i]);

            return input;
        }
        readonly string[] colorPatterns =
        [
            "{default}", "{darkred}", "{purple}", "{green}", "{lightgreen}", "{lime}", "{red}", "{grey}",
            "{olive}", "{a}", "{lightblue}", "{blue}", "{d}", "{pink}", "{darkorange}", "{orange}",
            "{white}", "{yellow}", "{magenta}", "{silver}", "{bluegrey}", "{lightred}", "{cyan}", "{gray}"
        ];
        readonly string[] colorReplacements =
        [
            "\x01", "\x02", "\x03", "\x04", "\x05", "\x06", "\x07", "\x08",
            "\x09", "\x0A", "\x0B", "\x0C", "\x0D", "\x0E", "\x0F", "\x10",
            "\x01", "\x09", "\x0E", "\x0A", "\x0D", "\x0F", "\x03", "\x08"
        ];

        readonly string[] FootStepsArray = [
            "T_Default.StepLeft",
            "CT_Default.StepLeft"
        ];

        readonly string[] HitSoundsArray = [
            "Player.DamageBody.AttackerFeedback",
            "Player.DamageBody.Onlooker",
            "Player.DamageBody.Victim",
            "Player.DamageBody.VictimFlesh",
            "Player.DamageBodyArmor.AttackerFeedback",
            "Player.DamageBodyArmor.Onlooker",
            "Player.DamageBodyArmor.Victim",
            "Player.DamageBodyArmor.OnlookerFlesh",
            "Player.DamageBodyArmor.AttackerFeedbackFlesh",
            "Player.DamageHeadShot.AttackerFeedback",
            "Player.DamageHeadShot.Onlooker",
            "Player.DamageHeadShot.Victim",
            "Player.DamageHeadShotArmor.AttackerFeedback",
            "Player.DamageHeadShotArmor.Onlooker",
            "Player.DamageHeadShotArmor.Victim",
            "Player.DamageFall",
            "Player.DamageFall.Fem",
            "UI.KillCard.1",
            "Player.Death",
            "Player.DeathTaser",
            "Player.DeathTaser_F",
            "Player.DeathBody.AttackerFeedback",
            "Player.DeathBody.Onlooker",
            "Player.DeathBody.Victim",
            "Player.DeathBody.Flesh",
            "Player.DeathBodyArmor.AttackerFeedback",
            "Player.DeathBodyArmor.Onlooker",
            "Player.DeathBodyArmor.Victim",
            "Player.DeathHeadShot.AttackerFeedback",
            "Player.DeathHeadShot.Onlooker",
            "Player.DeathHeadShot.Victim",
            "Player.DeathHeadShot.Spectator",
            "Player.DeathHeadShot.Victim.Dink",
            "Player.DeathHeadShot.Victim.Flesh",
            "Player.DeathHeadShotArmor.AttackerFeedback",
            "Player.DeathHeadShotArmor.Onlooker",
            "Player.DeathHeadShotArmor.Victim",
            "Player.DeathHeadShotArmor.Spectator",
            "Player.DeathHeadShot.AttackerFeedback.Flesh",
            "Player.DeathHeadShot.AttackerFeedback.Dink",
            "Player.DeathHeadShot.Flesh",
            "Player.DeathHeadShot.Dink"
        ];

        readonly string[] WeaponSoundsArray = [
            "Weapon_Knife.HitWall",
            "Weapon_Knife.Slash",
            "Weapon_Knife.Hit",
            "Weapon_Knife.Stab",
            "Weapon_sg556.ZoomIn",
            "Weapon_sg556.ZoomOut",
            "Weapon_AUG.ZoomIn",
            "Weapon_AUG.ZoomOut",
            "Weapon_SSG08.Zoom",
            "Weapon_SSG08.ZoomOut",
            "Weapon_SCAR20.Zoom",
            "Weapon_SCAR20.ZoomOut",
            "Weapon_G3SG1.Zoom",
            "Weapon_G3SG1.ZoomOut",
            "Weapon_AWP.Zoom",
            "Weapon_AWP.ZoomOut",
            "Weapon_Revolver.Prepare",
            "Weapon.AutoSemiAutoSwitch"
        ];

        private ILocalizerManager? GetLocalizer()
        {
            if (_localizer?.Instance is null)
            {
                _localizer = _modules.GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity);
            }
            return _localizer?.Instance;
        }

        private IClientPreference? GetClientPrefs()
        {
            if (_icp?.Instance is null)
            {
                _icp = _modules.GetOptionalSharpModuleInterface<IClientPreference>(IClientPreference.Identity);
                if (_icp?.Instance is { } instance) _callback = instance.ListenOnLoad(OnCookieLoad);
            }
            return _icp?.Instance;
        }
    }
}
